using System.Text;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class AvaPageInspectionService(
    ILogger<AvaPageInspectionService> logger) : IAvaPageInspectionService
{
    private static readonly string InspectionDirectory = Path.Combine("logs", "inspection");

    public async Task CaptureHomeAsync(
        IAutomationPage avaPage,
        IAutomationLocator cards,
        IReadOnlyCollection<Disciplina> disciplines,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(InspectionDirectory);

        var htmlPath = Path.Combine(InspectionDirectory, "ava-home.html");
        var screenshotPath = Path.Combine(InspectionDirectory, "ava-home.png");
        var treePath = Path.Combine(InspectionDirectory, "ava-home-tree.txt");

        try
        {
            var html = await avaPage.GetContentAsync(cancellationToken);
            await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8, cancellationToken);

            await HighlightCardsAsync(cards, cancellationToken);
            await avaPage.ScreenshotAsync(screenshotPath, cancellationToken);

            var tree = BuildTree(disciplines);
            await File.WriteAllTextAsync(treePath, tree, Encoding.UTF8, cancellationToken);

            logger.LogInformation(
                "Inspeção da página salva em {HtmlPath}, {ScreenshotPath} e {TreePath}",
                Path.GetFullPath(htmlPath),
                Path.GetFullPath(screenshotPath),
                Path.GetFullPath(treePath));

            if (disciplines.Count == 0 || disciplines.All(IsUnnamed))
            {
                logger.LogWarning(
                    "Nenhum nome de disciplina foi identificado; a screenshot contém os cards destacados");
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Geração dos artefatos de inspeção cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao gerar inspeção do AVA. Exceção completa: {ExceptionDetails}",
                exception.ToString());
        }
    }

    private static async Task HighlightCardsAsync(
        IAutomationLocator cards,
        CancellationToken cancellationToken)
    {
        var count = await cards.CountAsync(cancellationToken);

        for (var index = 0; index < count; index++)
        {
            await cards.Nth(index).HighlightAsync(cancellationToken);
        }
    }

    private static string BuildTree(IEnumerable<Disciplina> disciplines)
    {
        var builder = new StringBuilder();
        var index = 0;

        foreach (var discipline in disciplines)
        {
            index++;
            builder.AppendLine($"CARD {index}");
            builder.AppendLine($"Nome: {discipline.Nome}");
            builder.AppendLine($"Botões de acesso: {discipline.QuantidadeBotoes}");
            builder.AppendLine("Texto:");
            builder.AppendLine(discipline.Texto);
            builder.AppendLine();
        }

        if (index == 0)
        {
            builder.AppendLine("Nenhum card de disciplina encontrado.");
        }

        return builder.ToString();
    }

    private static bool IsUnnamed(Disciplina discipline) =>
        discipline.Nome.StartsWith(
            "Disciplina sem nome identificado",
            StringComparison.OrdinalIgnoreCase);
}
