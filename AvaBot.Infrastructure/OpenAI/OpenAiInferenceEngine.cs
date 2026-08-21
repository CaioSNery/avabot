using System.Diagnostics;
using System.Net;
using AvaBot.Application.Services;
using AvaBot.Domain.Exceptions;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Infrastructure.OpenAI;

public sealed class OpenAiInferenceEngine(
    IOpenAiClient client,
    QuestionBatchBuilder batchBuilder,
    InferenceValidator validator,
    InferenceCache cache,
    PromptRepository promptRepository,
    IOptions<OpenAiOptions> options,
    ILogger<OpenAiInferenceEngine> logger) : IInferenceEngine
{
    private readonly OpenAiOptions _options = options.Value;

    public async Task<IReadOnlyList<InferenceResultDto>> ExecuteAsync(
        InferenceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        var totalStopwatch = Stopwatch.StartNew();
        var batch = batchBuilder.Build(request);
        var results = new List<InferenceResultDto>(batch.Questions.Count);
        var prompt = await promptRepository.GetQuestionAnswerPromptAsync(
            batch.VisualMode,
            cancellationToken);

        logger.LogInformation(
            "Iniciando inferência por questão; PrimaryModel: {PrimaryModel}; FallbackModel: {FallbackModel}; limite de confiança: {Threshold}; perguntas: {QuestionCount}",
            _options.PrimaryModel,
            _options.FallbackModel,
            _options.FallbackConfidenceThreshold,
            batch.Questions.Count);

        foreach (var question in batch.Questions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryGetCachedResult(question, out var cachedResult))
            {
                results.Add(cachedResult!);
                continue;
            }

            var singleQuestionRequest = new InferenceRequestDto([question])
            {
                VisualMode = batch.VisualMode
            };

            logger.LogInformation(
                "[OpenAI] Questão {QuestionNumber:00} → PrimaryModel {Model}",
                question.QuestionNumber,
                _options.PrimaryModel);

            var primaryAttempt = await ExecuteModelAsync(
                _options.PrimaryModel,
                prompt,
                singleQuestionRequest,
                cancellationToken);

            if (primaryAttempt.Result is not null &&
                IsReliablePrimaryResult(primaryAttempt.Result, question))
            {
                cache.Set(question, _options.PrimaryModel, primaryAttempt.Result);
                results.Add(primaryAttempt.Result);
                logger.LogInformation(
                    "[OpenAI] Questão {QuestionNumber:00} → confiança {Confidence:F2} → ACEITA",
                    question.QuestionNumber,
                    primaryAttempt.Result.Confidence);
                continue;
            }

            LogFallbackReason(question.QuestionNumber, primaryAttempt);
            logger.LogInformation(
                "[OpenAI] Questão {QuestionNumber:00} → FallbackModel {Model}",
                question.QuestionNumber,
                _options.FallbackModel);

            var fallbackAttempt = await ExecuteModelAsync(
                _options.FallbackModel,
                prompt,
                singleQuestionRequest,
                cancellationToken);

            if (fallbackAttempt.Result is not null)
            {
                cache.Set(question, _options.FallbackModel, fallbackAttempt.Result);
                results.Add(fallbackAttempt.Result);
                logger.LogInformation(
                    "[OpenAI] Questão {QuestionNumber:00} → FallbackModel confiança {Confidence:F2} → {Decision}",
                    question.QuestionNumber,
                    fallbackAttempt.Result.Confidence,
                    IsAnswered(fallbackAttempt.Result) ? "ACEITA" : "REVISÃO MANUAL");
                continue;
            }

            logger.LogWarning(
                "[OpenAI] Questão {QuestionNumber:00} → FallbackModel inválido ({FailureReason}); questão encaminhada para revisão manual",
                question.QuestionNumber,
                fallbackAttempt.FailureReason);
            results.Add(CreateManualReviewResult(
                question.QuestionNumber,
                fallbackAttempt.FailureReason ?? "O fallback não retornou uma resposta utilizável."));
        }

        totalStopwatch.Stop();
        logger.LogInformation(
            "Inferência concluída em {ElapsedMilliseconds} ms; {ResultCount} resultado(s)",
            totalStopwatch.ElapsedMilliseconds,
            results.Count);

        return results
            .OrderBy(result => result.QuestionNumber)
            .ToArray();
    }

    private bool TryGetCachedResult(
        QuestionRequestDto question,
        out InferenceResultDto? result)
    {
        if (cache.TryGet(question, _options.PrimaryModel, out var primaryCached) &&
            primaryCached is not null &&
            IsReliablePrimaryResult(primaryCached, question))
        {
            logger.LogInformation(
                "[OpenAI] Questão {QuestionNumber:00} → cache do PrimaryModel → ACEITA",
                question.QuestionNumber);
            result = primaryCached;
            return true;
        }

        if (cache.TryGet(question, _options.FallbackModel, out var fallbackCached) &&
            fallbackCached is not null)
        {
            logger.LogInformation(
                "[OpenAI] Questão {QuestionNumber:00} → cache do FallbackModel → ACEITA",
                question.QuestionNumber);
            result = fallbackCached;
            return true;
        }

        result = null;
        return false;
    }

    private async Task<ModelAttempt> ExecuteModelAsync(
        string model,
        string prompt,
        InferenceRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var apiResult = await client.ExecuteAsync(
                model,
                prompt,
                request,
                cancellationToken);
            var inferredResults = validator.ValidateAndDeserialize(
                apiResult.OutputJson,
                request);

            return new ModelAttempt(inferredResults.Single(), null);
        }
        catch (InferenceValidationException exception)
        {
            return new ModelAttempt(null, exception.Message);
        }
        catch (Exception exception) when (IsRecoverableApiFailure(exception))
        {
            logger.LogWarning(
                exception,
                "Falha recuperável após a política HTTP do modelo {Model}",
                model);
            return new ModelAttempt(null, exception.Message);
        }
    }

    private bool IsReliablePrimaryResult(
        InferenceResultDto result,
        QuestionRequestDto question) =>
        IsAnswered(result) &&
        result.Confidence >= _options.FallbackConfidenceThreshold &&
        question.Alternatives.Any(alternative =>
            string.Equals(
                alternative.Id,
                result.SelectedAlternative,
                StringComparison.OrdinalIgnoreCase));

    private static bool IsAnswered(InferenceResultDto result) =>
        string.Equals(
            result.Status,
            InferenceStatuses.Answered,
            StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(result.SelectedAlternative);

    private void LogFallbackReason(int questionNumber, ModelAttempt attempt)
    {
        if (attempt.Result is null)
        {
            logger.LogInformation(
                "[OpenAI] Questão {QuestionNumber:00} → resposta primária inválida ({FailureReason}) → FALLBACK",
                questionNumber,
                attempt.FailureReason);
            return;
        }

        logger.LogInformation(
            "[OpenAI] Questão {QuestionNumber:00} → status {Status}; alternativa {SelectedAlternative}; confiança {Confidence:F2} → FALLBACK",
            questionNumber,
            attempt.Result.Status,
            attempt.Result.SelectedAlternative,
            attempt.Result.Confidence);
    }

    private static bool IsRecoverableApiFailure(Exception exception)
    {
        if (exception is TimeoutException)
        {
            return true;
        }

        if (exception is not HttpRequestException httpException)
        {
            return false;
        }

        return httpException.StatusCode is null or
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.PrimaryModel))
        {
            throw new InvalidOperationException(
                "Configure OpenAI:PrimaryModel no appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(_options.FallbackModel))
        {
            throw new InvalidOperationException(
                "Configure OpenAI:FallbackModel no appsettings.json.");
        }

        if (_options.FallbackConfidenceThreshold is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "OpenAI:FallbackConfidenceThreshold deve estar entre zero e um.");
        }
    }

    private static InferenceResultDto CreateManualReviewResult(
        int questionNumber,
        string validationMessage) =>
        new(
            questionNumber,
            InferenceStatuses.ManualReviewRequired,
            null,
            0,
            "A resposta automática foi descartada para preservar a tentativa.",
            MissingData: "A questão requer conferência e preenchimento manual.",
            Warning: validationMessage,
            Validation: new InferenceValidationDto(false, false));

    private sealed record ModelAttempt(
        InferenceResultDto? Result,
        string? FailureReason);
}
