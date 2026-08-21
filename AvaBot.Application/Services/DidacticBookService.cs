using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class DidacticBookService(
    IActivityProgressStore progressStore,
    IOptions<DidacticBookOptions> options,
    ILogger<DidacticBookService> logger) : IDidacticBookService
{
    private const string ContentSelector = "#ct-cards-list";
    private const string BookPageTitleSelector =
        "#ct-cards-list .ct-section-active-topic h1:has-text('Livro Didático')";
    private const string BookMenuSelector =
        "#ctmenu .timeline > li > .timeline-item > a:has-text('Livro Didático')";
    private const string BookItemSelector =
        "#ct-cards-list > .ctgrid-row > .ct-resource-list .ct-list:has(.ct-list-title h3 a)";
    private const string BookLinkSelector = ".ct-list-title h3 a";
    private const string CompletionIndicatorSelector =
        ".ct-list-title h3 i[completion]";
    private readonly DidacticBookOptions _options = options.Value;

    public async Task<int> VisitPendingAsync(
        IAutomationPage disciplinePage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(disciplinePage);

        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Visita aos livros didáticos desabilitada em DidacticBook:Enabled");
            return 0;
        }

        ValidateOptions();

        try
        {
            logger.LogInformation("DidacticBookService iniciado");
            await EnsureBookPageAsync(disciplinePage, cancellationToken);
            await WaitForCompletionStateAsync(disciplinePage, cancellationToken);

            var items = disciplinePage.GetLocator(BookItemSelector);
            var itemCount = await items.CountAsync(cancellationToken);
            var visitedCount = 0;

            logger.LogInformation(
                "Quantidade de livros didáticos encontrados: {BookCount}",
                itemCount);

            for (var index = 0; index < itemCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = items.Nth(index);
                var link = item.GetLocator(BookLinkSelector).Nth(0);
                var name = NormalizeText(await link.InnerTextAsync(cancellationToken));
                var href = await link.GetAttributeAsync("href", cancellationToken);
                var identifier = !string.IsNullOrWhiteSpace(href)
                    ? href.Trim()
                    : $"livro-didatico::{name}";
                var completionState = await CompletionStatusReader.ReadAsync(
                    item.GetLocator(CompletionIndicatorSelector).Nth(0),
                    cancellationToken);
                var locallyCompleted = await progressStore.IsCompletedAsync(
                    identifier,
                    cancellationToken);

                logger.LogInformation(
                    "Livro encontrado: {BookName}; estado AVA: {CompletionStatus}; evidência: {CompletionEvidence}; registro local: {LocallyCompleted}",
                    name,
                    completionState.Status,
                    completionState.Evidence,
                    locallyCompleted);

                if (locallyCompleted ||
                    completionState.Status == ActivityCompletionStatus.Completed)
                {
                    if (!locallyCompleted)
                    {
                        await progressStore.MarkCompletedAsync(
                            identifier,
                            cancellationToken);
                    }

                    logger.LogInformation(
                        "Ignorando livro didático já visitado: {BookName}",
                        name);
                    continue;
                }

                await VisitAndReturnAsync(
                    disciplinePage,
                    link,
                    name,
                    cancellationToken);
                await progressStore.MarkCompletedAsync(identifier, cancellationToken);
                visitedCount++;

                logger.LogInformation(
                    "Livro didático visitado e registrado no progresso local: {BookName}",
                    name);
            }

            logger.LogInformation(
                "DidacticBookService concluído. Livros encontrados: {BookCount}; visitados nesta execução: {VisitedCount}",
                itemCount,
                visitedCount);
            return visitedCount;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha ao processar livros didáticos. URL atual: {CurrentUrl}",
                disciplinePage.Url);
            throw;
        }
    }

    private async Task EnsureBookPageAsync(
        IAutomationPage disciplinePage,
        CancellationToken cancellationToken)
    {
        await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);

        if (await disciplinePage.GetLocator(BookPageTitleSelector)
                .CountAsync(cancellationToken) > 0)
        {
            logger.LogInformation("Página Livro Didático já está aberta");
            return;
        }

        var menuLinks = disciplinePage.GetLocator(BookMenuSelector);
        var menuLinkCount = await menuLinks.CountAsync(cancellationToken);

        if (menuLinkCount == 0)
        {
            throw new InvalidOperationException(
                "O item de menu 'Livro Didático' não foi encontrado.");
        }

        var previousUrl = disciplinePage.Url;
        logger.LogInformation("Abrindo a página Livro Didático pelo menu lateral");
        await menuLinks.Nth(0).ClickAsync(cancellationToken);
        await disciplinePage.WaitForUrlChangeAsync(previousUrl, cancellationToken);
        await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);
        await disciplinePage.WaitForVisibleAsync(ContentSelector, cancellationToken);
    }

    private async Task WaitForCompletionStateAsync(
        IAutomationPage page,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Aguardando {RenderDelay} ms para o AVA atualizar os estados dos livros",
            _options.CompletionRenderDelay);
        await page.WaitForClientRenderAsync(
            TimeSpan.FromMilliseconds(_options.CompletionRenderDelay),
            cancellationToken);
    }

    private async Task VisitAndReturnAsync(
        IAutomationPage disciplinePage,
        IAutomationLocator link,
        string name,
        CancellationToken cancellationToken)
    {
        var bookPageUrl = disciplinePage.Url;

        logger.LogInformation("Abrindo livro didático: {BookName}", name);
        await link.ClickAsync(cancellationToken);
        await disciplinePage.WaitForUrlChangeAsync(bookPageUrl, cancellationToken);
        await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);
        await disciplinePage.WaitForNetworkIdleAsync(cancellationToken);
        logger.LogInformation(
            "Livro aberto: {BookName}; URL: {BookUrl}; título: {BookTitle}",
            name,
            disciplinePage.Url,
            await disciplinePage.GetTitleAsync(cancellationToken));

        await disciplinePage.WaitForClientRenderAsync(
            TimeSpan.FromMilliseconds(_options.VisitDuration),
            cancellationToken);

        logger.LogInformation("Retornando do livro didático: {BookName}", name);
        await disciplinePage.GoBackAsync(cancellationToken);
        await disciplinePage.WaitForDomContentLoadedAsync(cancellationToken);
        await disciplinePage.WaitForVisibleAsync(ContentSelector, cancellationToken);
        await disciplinePage.WaitForClientRenderAsync(
            TimeSpan.FromMilliseconds(_options.CompletionRenderDelay),
            cancellationToken);
        logger.LogInformation(
            "Retorno à página Livro Didático concluído. URL: {BookPageUrl}",
            disciplinePage.Url);
    }

    private void ValidateOptions()
    {
        if (_options.CompletionRenderDelay <= 0)
        {
            throw new InvalidOperationException(
                "DidacticBook:CompletionRenderDelay deve ser maior que zero.");
        }

        if (_options.VisitDuration <= 0)
        {
            throw new InvalidOperationException(
                "DidacticBook:VisitDuration deve ser maior que zero.");
        }
    }

    private static string NormalizeText(string text) =>
        string.Join(
            ' ',
            text.Split(
                [' ', '\r', '\n', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
