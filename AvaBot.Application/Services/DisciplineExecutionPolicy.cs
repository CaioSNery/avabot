using System.Globalization;
using System.Text;

namespace AvaBot.Application.Services;

public sealed class DisciplineExecutionPolicy
{
    public bool ShouldSkipWithoutOpening(string disciplineName) =>
        Normalize(disciplineName).Contains(
            "projeto de extensao",
            StringComparison.Ordinal);

    public bool IsExcludedLinearTopic(string topicName) =>
        Normalize(topicName).Equals(
            "certificado",
            StringComparison.Ordinal);

    public bool AllowsLocalCompletionForLinearTopic(string topicName) =>
        !string.IsNullOrWhiteSpace(topicName) &&
        !IsExcludedLinearTopic(topicName);

    public bool RequiresLiveCompletionForLinearTopic(
        string disciplineName,
        string topicName)
    {
        var normalizedDiscipline = Normalize(disciplineName);
        var normalizedTopic = Normalize(topicName);

        return normalizedDiscipline.Contains(
                   "pensamento cientifico",
                   StringComparison.Ordinal) &&
               normalizedTopic.StartsWith(
                   "unidade de ensino",
                   StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return string.Join(
            ' ',
            builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(
                    [' ', '\r', '\n', '\t'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));
    }
}
