using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvaBot.Domain.Models;

namespace AvaBot.Infrastructure.OpenAI;

public sealed class InferenceCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, InferenceResultDto> _entries = new();

    public bool TryGet(
        QuestionRequestDto question,
        string model,
        out InferenceResultDto? result) =>
        _entries.TryGetValue(CreateKey(question, model), out result);

    public void Set(
        QuestionRequestDto question,
        string model,
        InferenceResultDto result) =>
        _entries[CreateKey(question, model)] = result;

    public static string CreateKey(QuestionRequestDto question, string model)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var keyData = new
        {
            statement = question.Statement.Trim(),
            alternatives = question.Alternatives.Select(alternative => new
            {
                id = alternative.Id.Trim(),
                text = alternative.Text.Trim()
            }),
            formulas = question.Formulas,
            tables = question.Tables,
            screenshotHash = string.IsNullOrWhiteSpace(question.ScreenshotDataUrl)
                ? string.Empty
                : Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(question.ScreenshotDataUrl))),
            model = model.Trim()
        };

        var bytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(keyData, SerializerOptions));

        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
