using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace AvaBot.Infrastructure.OpenAI;

public sealed class OpenAiClient : IOpenAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlySet<HttpStatusCode> RetriableStatusCodes =
        new HashSet<HttpStatusCode>
        {
            HttpStatusCode.TooManyRequests,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable
        };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public OpenAiClient(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _retryPolicy = CreateRetryPolicy();
    }

    public async Task<OpenAiClientResult> ExecuteAsync(
        string model,
        string prompt,
        InferenceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Montando payload multimodal; questões: {QuestionCount}; imagens completas: {ImageCount}; bytes estimados de imagem: {ImageBytes}",
            request.Questions.Count,
            request.Questions.Count(question => question.ScreenshotPresent),
            request.Questions.Sum(question => question.ScreenshotBytes));

        var payloadJson = JsonSerializer.Serialize(
            CreateRequestPayload(model, prompt, request),
            SerializerOptions);
        var stopwatch = Stopwatch.StartNew();

        HttpResponseMessage response;

        try
        {
            response = await _retryPolicy.ExecuteAsync(
                async token =>
                {
                    using var request = CreateRequest(payloadJson);
                    return await _httpClient.SendAsync(request, token);
                },
                cancellationToken);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"A chamada à Responses API excedeu {_options.RequestTimeoutSeconds} segundos e não será reenviada automaticamente.",
                exception);
        }

        using (response)
        {
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"A Responses API retornou HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                    null,
                    response.StatusCode);
            }

            var result = ParseResponse(responseJson);

            _logger.LogDebug(
                "Resposta estruturada bruta recebida da Responses API: {OutputJson}",
                result.OutputJson);

            _logger.LogInformation(
                "[OpenAI] Modelo {Model}; tempo: {ElapsedMilliseconds} ms; InputTokens: {InputTokens}; OutputTokens: {OutputTokens}; TotalTokens: {TotalTokens}",
                model,
                stopwatch.ElapsedMilliseconds,
                result.InputTokens,
                result.OutputTokens,
                result.TotalTokens);

            return result;
        }
    }

    private AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy() =>
        Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(response => RetriableStatusCodes.Contains(response.StatusCode))
            .WaitAndRetryAsync(
                retryCount: _options.RetryCount,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
                onRetry: (outcome, delay, attempt, _) =>
                {
                    outcome.Result?.Dispose();
                    _logger.LogWarning(
                        "Nova tentativa {Attempt} da chamada à OpenAI em {DelaySeconds} segundos; status: {StatusCode}; exceção: {ExceptionType}",
                        attempt,
                        delay.TotalSeconds,
                        outcome.Result?.StatusCode,
                        outcome.Exception?.GetType().Name);
                });

    private HttpRequestMessage CreateRequest(string payloadJson)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    internal object CreateRequestPayload(
        string model,
        string prompt,
        InferenceRequestDto request)
    {
        var hasVisualInput = request.Questions.Any(question =>
            !string.IsNullOrWhiteSpace(question.ScreenshotDataUrl));
        var inputText = hasVisualInput
            ? "Use o texto estruturado como fonte principal e a imagem para " +
              "recuperar fórmulas, tabelas, gráficos ou informações visuais ausentes. " +
              "Se houver divergência causada por falha de OCR, priorize o conteúdo " +
              "claramente visível na imagem.\n\n" +
              JsonSerializer.Serialize(request, SerializerOptions)
            : JsonSerializer.Serialize(request, SerializerOptions);
        var content = new List<object>
        {
            new
            {
                type = "input_text",
                text = inputText
            }
        };

        foreach (var question in request.Questions.Where(question =>
                     !string.IsNullOrWhiteSpace(question.ScreenshotDataUrl)))
        {
            content.Add(new
            {
                type = "input_text",
                text = $"Imagem associada exclusivamente à questão {question.QuestionNumber}."
            });
            content.Add(new
            {
                type = "input_image",
                image_url = question.ScreenshotDataUrl,
                detail = "high"
            });
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["instructions"] = prompt,
            ["input"] = new[]
            {
                new
                {
                    role = "user",
                    content
                }
            },
            ["max_output_tokens"] = _options.MaxOutputTokens,
            ["reasoning"] = new
            {
                effort = _options.ReasoningEffort
            },
            ["text"] = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "inference_results",
                    strict = true,
                    schema = CreateResponseSchema()
                }
            }
        };

        if (_options.Temperature > 0)
        {
            payload["temperature"] = _options.Temperature;
        }

        return payload;
    }

    private static object CreateResponseSchema() => new
    {
        type = "object",
        properties = new
        {
            results = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        questionNumber = new { type = "integer" },
                        status = new
                        {
                            type = "string",
                            @enum = new[]
                            {
                                InferenceStatuses.Answered,
                                InferenceStatuses.InsufficientData
                            }
                        },
                        selectedAlternative = new { type = new[] { "string", "null" } },
                        confidence = new { type = "number", minimum = 0, maximum = 1 },
                        calculatedAnswer = new { type = new[] { "string", "null" } },
                        reasoning = new { type = "string" },
                        missingData = new { type = new[] { "string", "null" } },
                        warning = new { type = new[] { "string", "null" } },
                        validation = new
                        {
                            type = "object",
                            properties = new
                            {
                                hasSufficientData = new { type = "boolean" },
                                alternativeMatchesReasoning = new { type = "boolean" }
                            },
                            required = new[]
                            {
                                "hasSufficientData",
                                "alternativeMatchesReasoning"
                            },
                            additionalProperties = false
                        }
                    },
                    required = new[]
                    {
                        "questionNumber",
                        "status",
                        "selectedAlternative",
                        "confidence",
                        "calculatedAnswer",
                        "reasoning",
                        "missingData",
                        "warning",
                        "validation"
                    },
                    additionalProperties = false
                }
            }
        },
        required = new[] { "results" },
        additionalProperties = false
    };

    private static OpenAiClientResult ParseResponse(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var outputText = FindOutputText(root);

        var inputTokens = 0;
        var outputTokens = 0;
        var totalTokens = 0;

        if (root.TryGetProperty("usage", out var usage))
        {
            inputTokens = GetInt32(usage, "input_tokens");
            outputTokens = GetInt32(usage, "output_tokens");
            totalTokens = GetInt32(usage, "total_tokens");
        }

        return new OpenAiClientResult(
            outputText,
            inputTokens,
            outputTokens,
            totalTokens);
    }

    private static string FindOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var outputItems))
        {
            throw new InvalidOperationException(
                "A resposta da OpenAI não contém o campo 'output'.");
        }

        foreach (var outputItem in outputItems.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var contentItems))
            {
                continue;
            }

            foreach (var contentItem in contentItems.EnumerateArray())
            {
                if (contentItem.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    contentItem.TryGetProperty("text", out var text))
                {
                    return text.GetString()
                        ?? throw new InvalidOperationException(
                            "A OpenAI retornou um output_text vazio.");
                }
            }
        }

        throw new InvalidOperationException(
            "A resposta da OpenAI não contém conteúdo do tipo 'output_text'.");
    }

    private static int GetInt32(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;

    private void ValidateConfiguration(string model)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Configure OpenAI:ApiKey no appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException(
                "O modelo da chamada à Responses API não foi configurado.");
        }

        if (_options.MaxOutputTokens <= 0)
        {
            throw new InvalidOperationException(
                "OpenAI:MaxOutputTokens deve ser maior que zero.");
        }

        if (_options.Temperature is < 0 or > 2)
        {
            throw new InvalidOperationException(
                "OpenAI:Temperature deve estar entre 0 e 2.");
        }

        var supportedReasoningEfforts = new[] { "minimal", "low", "medium", "high" };
        if (!supportedReasoningEfforts.Contains(
                _options.ReasoningEffort,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "OpenAI:ReasoningEffort deve ser minimal, low, medium ou high para o modelo gpt-5.");
        }

        if (_options.RequestTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException(
                "OpenAI:RequestTimeoutSeconds deve ser maior que zero.");
        }

        if (_options.RetryCount is < 0 or > 5)
        {
            throw new InvalidOperationException(
                "OpenAI:RetryCount deve estar entre zero e cinco.");
        }
    }
}
