using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class UnitNavigationService(
    ILogger<UnitNavigationService> logger) : IUnitNavigationService
{
    private const string ExpandedAttribute = "aria-expanded";
    private const string UnitContainerSelector = "xpath=ancestor::li[1]";
    private const string ChildSelector = ".timeline-menu li > a";
    private static readonly TimeSpan RenderTimeout = TimeSpan.FromSeconds(10);

    public async Task<UnidadeEnsino> ExecuteAsync(
        UnidadeEnsino unit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var stateBefore = unit.Expandida;

        logger.LogInformation("Unidade selecionada: {UnitName}", unit.Nome);
        logger.LogInformation(
            "Estado antes da navegação: {UnitState}",
            stateBefore ? "Expandida" : "Recolhida");

        if (!stateBefore)
        {
            logger.LogInformation("Expandindo a unidade {UnitName}", unit.Nome);
            await unit.Toggle.ClickAsync(cancellationToken);

            await unit.Toggle.WaitForAttributeAsync(
                ExpandedAttribute,
                bool.TrueString.ToLowerInvariant(),
                RenderTimeout,
                cancellationToken);

            logger.LogInformation("DOM estabilizado após a expansão da unidade");
        }
        else
        {
            logger.LogInformation("A unidade já estava expandida; clique não necessário");
        }

        var children = unit.Toggle
            .GetLocator(UnitContainerSelector)
            .GetLocator(ChildSelector);
        var childCount = await children.CountAsync(cancellationToken);

        if (childCount > 0)
        {
            var firstChildVisible = await children.Nth(0).TryWaitForVisibleAsync(
                RenderTimeout,
                cancellationToken);

            if (!firstChildVisible)
            {
                throw new InvalidOperationException(
                    $"Os elementos filhos de '{unit.Nome}' não ficaram visíveis.");
            }
        }

        var expandedValue = await unit.Toggle.GetAttributeAsync(
            ExpandedAttribute,
            cancellationToken);
        unit.Expandida = bool.TryParse(expandedValue, out var expanded) && expanded;

        logger.LogInformation(
            "Estado depois da navegação: {UnitState}",
            unit.Expandida ? "Expandida" : "Recolhida");
        logger.LogInformation(
            "Quantidade de elementos filhos encontrados: {ChildCount}",
            childCount);

        return unit;
    }
}
