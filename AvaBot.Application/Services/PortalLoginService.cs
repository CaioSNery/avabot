using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class PortalLoginService(
    IOptions<PortalOptions> options,
    ILogger<PortalLoginService> logger) : IPortalLoginService
{
    private const string CpfSelector = "#username";
    private const string PasswordSelector = "#login-pass";
    private const string SubmitButtonSelector = "button[data-testid='submit-button']";
    private const string TabKey = "Tab";

    private readonly PortalOptions _options = options.Value;

    public async Task AuthenticateAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        ValidateCredentials();

        try
        {
            logger.LogInformation("Iniciando autenticação em duas etapas");

            await PreencherCpfAsync(page, cancellationToken);
            await PreencherSenhaAsync(page, cancellationToken);

            logger.LogInformation("Credenciais enviadas com sucesso; autenticação concluída");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha durante a autenticação. Exceção completa: {ExceptionDetails}",
                exception.ToString());
            throw;
        }
    }

    private async Task PreencherCpfAsync(
        IAutomationPage page,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Aguardando o campo CPF ficar visível: {Selector}", CpfSelector);
        await page.WaitForVisibleAsync(CpfSelector, cancellationToken);
        logger.LogInformation("Campo CPF visível");

        logger.LogInformation("Preenchendo o campo CPF");
        await page.FillAsync(CpfSelector, _options.Login, cancellationToken);
        logger.LogInformation("Campo CPF preenchido");

        logger.LogInformation("Pressionando TAB no campo CPF");
        await page.PressAsync(CpfSelector, TabKey, cancellationToken);
        logger.LogInformation("TAB pressionado no campo CPF");

        await WaitForButtonAsync(page, "Avançar", cancellationToken);
        await ClickButtonAsync(page, "Avançar", cancellationToken);
    }

    private async Task PreencherSenhaAsync(
        IAutomationPage page,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Aguardando o campo de senha ficar visível: {Selector}", PasswordSelector);
        await page.WaitForVisibleAsync(PasswordSelector, cancellationToken);
        logger.LogInformation("Campo de senha visível");

        logger.LogInformation("Preenchendo o campo de senha");
        await page.FillAsync(PasswordSelector, _options.Senha, cancellationToken);
        logger.LogInformation("Campo de senha preenchido");

        logger.LogInformation("Pressionando TAB no campo de senha");
        await page.PressAsync(PasswordSelector, TabKey, cancellationToken);
        logger.LogInformation("TAB pressionado no campo de senha");

        await WaitForButtonAsync(page, "Entrar", cancellationToken);
        await ClickButtonAsync(page, "Entrar", cancellationToken);
    }

    private async Task WaitForButtonAsync(
        IAutomationPage page,
        string buttonName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Localizando o botão {ButtonName}: {Selector}",
            buttonName,
            SubmitButtonSelector);

        await page.WaitForVisibleAsync(SubmitButtonSelector, cancellationToken);
        logger.LogInformation("Botão {ButtonName} encontrado", buttonName);

        var isVisible = await page.IsVisibleAsync(SubmitButtonSelector, cancellationToken);
        var isEnabled = await page.IsEnabledAsync(SubmitButtonSelector, cancellationToken);
        var isDisabled = await page.IsDisabledAsync(SubmitButtonSelector, cancellationToken);

        logger.LogInformation("Botão {ButtonName} visível: {IsVisible}", buttonName, isVisible);
        logger.LogInformation("Botão {ButtonName} habilitado: {IsEnabled}", buttonName, isEnabled);
        logger.LogInformation("Botão {ButtonName} desabilitado: {IsDisabled}", buttonName, isDisabled);
    }

    private async Task ClickButtonAsync(
        IAutomationPage page,
        string buttonName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "URL antes do clique no botão {ButtonName}: {CurrentUrl}",
            buttonName,
            page.Url);

        logger.LogInformation("Clicando no botão {ButtonName}", buttonName);
        await page.ClickAsync(SubmitButtonSelector, cancellationToken);
        logger.LogInformation("Clique no botão {ButtonName} concluído", buttonName);

        logger.LogInformation(
            "URL depois do clique no botão {ButtonName}: {CurrentUrl}",
            buttonName,
            page.Url);
    }

    private void ValidateCredentials()
    {
        if (string.IsNullOrWhiteSpace(_options.Login) || string.IsNullOrWhiteSpace(_options.Senha))
        {
            throw new InvalidOperationException(
                "Informe Portal:Login e Portal:Senha no appsettings.json.");
        }
    }
}

