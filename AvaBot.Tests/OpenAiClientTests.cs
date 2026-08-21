using System.Net;
using System.Text.Json;
using AvaBot.Domain.Models;
using AvaBot.Infrastructure.OpenAI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvaBot.Tests;

public sealed class OpenAiClientTests
{
    [Fact]
    public async Task ExecuteAsync_ParsesStructuredResponseWithoutInternet()
    {
        const string responseJson =
            """
            {
              "output": [
                {
                  "content": [
                    {
                      "type": "output_text",
                      "text": "{\"results\":[{\"questionNumber\":1,\"status\":\"answered\",\"selectedAlternative\":\"A\",\"confidence\":0.9,\"calculatedAnswer\":\"A\",\"reasoning\":\"Motivo\",\"missingData\":null,\"warning\":null,\"validation\":{\"hasSufficientData\":true,\"alternativeMatchesReasoning\":true}}]}"
                    }
                  ]
                }
              ],
              "usage": {"input_tokens": 10, "output_tokens": 5, "total_tokens": 15}
            }
            """;
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, responseJson);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var options = Options.Create(new OpenAiOptions
        {
            ApiKey = "test-key",
            PrimaryModel = "gpt-5-mini",
            FallbackModel = "gpt-5",
            MaxOutputTokens = 100
        });
        var client = new OpenAiClient(
            httpClient,
            options,
            NullLogger<OpenAiClient>.Instance);

        var result = await client.ExecuteAsync(
            "gpt-5-mini",
            "Prompt de teste",
            new InferenceRequestDto([TestQuestionFactory.Create(1)]));

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(15, result.TotalTokens);
        Assert.Contains("selectedAlternative", result.OutputJson);
    }

    [Fact]
    public async Task ExecuteAsync_SendsStructuredTextAndQuestionScreenshotAsMultimodalInput()
    {
        const string responseJson =
            """
            {"output":[{"content":[{"type":"output_text","text":"{\"results\":[]}"}]}],"usage":{}}
            """;
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, responseJson);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var client = new OpenAiClient(
            httpClient,
            Options.Create(new OpenAiOptions
            {
                ApiKey = "test-key",
                PrimaryModel = "gpt-5-mini",
                FallbackModel = "gpt-5",
                MaxOutputTokens = 100
            }),
            NullLogger<OpenAiClient>.Instance);
        var baseQuestion = TestQuestionFactory.Create(1);
        var request = new InferenceRequestDto(
        [
            new QuestionRequestDto
            {
                QuestionNumber = 1,
                Statement = baseQuestion.Statement,
                Alternatives = baseQuestion.Alternatives,
                Formulas = ["a_n = a_(n-1) + 2n"],
                Tables = [new TableRequestDto(["n"], [["1"]])],
                ScreenshotDataUrl = "data:image/png;base64,AQID"
            }
        ]);

        await client.ExecuteAsync("gpt-5-mini", "Prompt", request);

        using var payload = JsonDocument.Parse(handler.LastRequestBody!);
        var content = payload.RootElement
            .GetProperty("input")[0]
            .GetProperty("content");
        Assert.Equal("input_text", content[0].GetProperty("type").GetString());
        var inputText = content[0].GetProperty("text").GetString()!;
        using var structuredInput = JsonDocument.Parse(
            inputText[(inputText.IndexOf("\n\n", StringComparison.Ordinal) + 2)..]);
        Assert.Equal(
            "a_n = a_(n-1) + 2n",
            structuredInput.RootElement
                .GetProperty("questions")[0]
                .GetProperty("formulas")[0]
                .GetString());
        Assert.Equal("input_text", content[1].GetProperty("type").GetString());
        Assert.Equal(
            "Imagem associada exclusivamente à questão 1.",
            content[1].GetProperty("text").GetString());
        Assert.Equal("input_image", content[2].GetProperty("type").GetString());
        Assert.Equal(
            "data:image/png;base64,AQID",
            content[2].GetProperty("image_url").GetString());
        Assert.Equal("high", content[2].GetProperty("detail").GetString());

        var resultProperties = payload.RootElement
            .GetProperty("text")
            .GetProperty("format")
            .GetProperty("schema")
            .GetProperty("properties")
            .GetProperty("results")
            .GetProperty("items")
            .GetProperty("properties");
        Assert.True(resultProperties.TryGetProperty("missingData", out _));
        Assert.True(resultProperties.TryGetProperty("validation", out _));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotAddImageOrVisualInstructionWithoutScreenshot()
    {
        const string responseJson =
            """
            {"output":[{"content":[{"type":"output_text","text":"{\"results\":[]}"}]}],"usage":{}}
            """;
        var handler = new MockHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, responseJson);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.com/v1/")
        };
        var client = new OpenAiClient(
            httpClient,
            Options.Create(new OpenAiOptions
            {
                ApiKey = "test-key",
                PrimaryModel = "gpt-5-mini",
                FallbackModel = "gpt-5",
                MaxOutputTokens = 100
            }),
            NullLogger<OpenAiClient>.Instance);
        var request = new InferenceRequestDto([TestQuestionFactory.Create(1)]);

        await client.ExecuteAsync("gpt-5-mini", "Prompt geral", request);

        using var payload = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(
            "gpt-5-mini",
            payload.RootElement.GetProperty("model").GetString());
        var content = payload.RootElement
            .GetProperty("input")[0]
            .GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("input_text", content[0].GetProperty("type").GetString());
        var inputText = content[0].GetProperty("text").GetString()!;
        Assert.StartsWith("{", inputText);
        Assert.DoesNotContain("priorize o conteúdo", inputText);
    }
}
