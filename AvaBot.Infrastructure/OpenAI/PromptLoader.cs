namespace AvaBot.Infrastructure.OpenAI;

public sealed class PromptLoader
{
    private readonly string _promptsDirectory;

    public PromptLoader()
        : this(Path.Combine(AppContext.BaseDirectory, "OpenAI", "Prompts"))
    {
    }

    public PromptLoader(string promptsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(promptsDirectory);
        _promptsDirectory = Path.GetFullPath(promptsDirectory);
    }

    public async Task<string> LoadAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new ArgumentException("O nome do prompt não pode conter caminhos.", nameof(fileName));
        }

        var promptPath = Path.Combine(_promptsDirectory, fileName);

        if (!File.Exists(promptPath))
        {
            throw new FileNotFoundException(
                $"O arquivo de prompt '{fileName}' não foi encontrado.",
                promptPath);
        }

        var prompt = await File.ReadAllTextAsync(promptPath, cancellationToken);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException(
                $"O arquivo de prompt '{fileName}' está vazio.");
        }

        return prompt.Trim();
    }
}
