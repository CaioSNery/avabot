using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class DisciplinaNavigationService(
    ILogger<DisciplinaNavigationService> logger) : IDisciplinaNavigationService
{
    public async Task ExecuteAsync(
        IAutomationPage avaPage,
        Disciplina disciplina,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(avaPage);
        ArgumentNullException.ThrowIfNull(disciplina);

        try
        {
            logger.LogInformation(
                "DisciplinaNavigationService iniciado para {DisciplineName}",
                disciplina.Nome);

            if (await disciplina.AccessButton.CountAsync(cancellationToken) == 0)
            {
                throw new InvalidOperationException(
                    $"O botão de acesso da disciplina '{disciplina.Nome}' não foi encontrado.");
            }

            logger.LogInformation(
                "Clicando no botão 'Acessar a Disciplina' de {DisciplineName}",
                disciplina.Nome);

            var previousUrl = avaPage.Url;
            await disciplina.AccessButton.ClickAsync(cancellationToken);
            logger.LogInformation("Clique no acesso da disciplina realizado");

            await avaPage.WaitForUrlChangeAsync(previousUrl, cancellationToken);
            logger.LogInformation(
                "Navegação confirmada. URL alterada de {PreviousUrl} para {CurrentUrl}",
                previousUrl,
                avaPage.Url);

            await avaPage.WaitForDomContentLoadedAsync(cancellationToken);
            logger.LogInformation("DOMContentLoaded concluído para a disciplina");

            await avaPage.WaitForNetworkIdleAsync(cancellationToken);
            logger.LogInformation("NetworkIdle concluído para a disciplina");

            logger.LogInformation("Disciplina aberta: {DisciplineName}", disciplina.Nome);
            logger.LogInformation("URL da disciplina: {DisciplineUrl}", avaPage.Url);
            logger.LogInformation(
                "Título da página: {PageTitle}",
                await avaPage.GetTitleAsync(cancellationToken));
            logger.LogInformation(
                "DisciplinaNavigationService concluído; página liberada para SectionExplorerService");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Navegação da disciplina cancelada");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao abrir a disciplina selecionada. Exceção completa: {ExceptionDetails}",
                exception.ToString());
            throw;
        }
    }
}
