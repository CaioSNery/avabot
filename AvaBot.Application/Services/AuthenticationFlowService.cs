using AvaBot.Domain.Interfaces;
using AvaBot.Shared;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class AuthenticationFlowService(
    IPortalLoginService portalLoginService,
    ILogger<AuthenticationFlowService> logger) : IAuthenticationFlowService
{
    private const string SkipMfaButtonName = "Pular por enquanto";
    private const string MfaScreenshotPath = "logs/screenshots/mfa-before-click.png";
    private static readonly TimeSpan MfaDetectionTimeout = TimeSpan.FromSeconds(10);

    public async Task<OperationResult> ExecuteAsync(
        IAutomationPage page,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Etapa Login iniciada");
            await portalLoginService.AuthenticateAsync(page, cancellationToken);
            logger.LogInformation("Etapa Login concluída");

            logger.LogInformation("Aguardando NetworkIdle após clicar em Entrar");
            await page.WaitForLoadAsync(cancellationToken);
            logger.LogInformation("NetworkIdle alcançado após clicar em Entrar");

            logger.LogInformation("Etapa MFA opcional iniciada");
            await HandleOptionalMfaAsync(page, cancellationToken);
            logger.LogInformation("Etapa MFA opcional concluída");

            logger.LogInformation("Cookies de sessão mantidos no contexto atual do navegador");
            return OperationResult.Success("Login e etapa opcional de MFA concluídos.");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Fluxo de autenticação cancelado");
            await RegisterFailureDiagnosticsAsync(page);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Falha durante o fluxo de entrada. Exceção completa: {ExceptionDetails}",
                exception.ToString());

            await RegisterFailureDiagnosticsAsync(page);
            return OperationResult.Failure("Falha ao concluir o fluxo de entrada no AVA.");
        }
    }

    private async Task HandleOptionalMfaAsync(
        IAutomationPage page,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Verificando se a tela opcional de MFA foi apresentada");
        logger.LogInformation("URL atual antes da detecção de MFA: {CurrentUrl}", page.Url);

        var pageTitle = await page.GetTitleAsync(cancellationToken);
        logger.LogInformation("Título atual antes da detecção de MFA: {PageTitle}", pageTitle);

        var skipMfaButton = page.GetButtonByRole(SkipMfaButtonName);

        logger.LogInformation(
            "Aguardando por até {TimeoutSeconds} segundos o botão '{ButtonName}'",
            MfaDetectionTimeout.TotalSeconds,
            SkipMfaButtonName);

        var isVisible = await skipMfaButton.TryWaitForVisibleAsync(
            MfaDetectionTimeout,
            cancellationToken);

        var buttonCount = await skipMfaButton.CountAsync(cancellationToken);
        logger.LogInformation(
            "Quantidade de botões '{ButtonName}' encontrados: {ButtonCount}",
            SkipMfaButtonName,
            buttonCount);

        if (!isVisible || buttonCount == 0)
        {
            logger.LogInformation("Tela MFA realmente não apresentada");
            return;
        }

        logger.LogInformation("Tela MFA encontrada");
        await CaptureMfaScreenshotAsync(page);

        logger.LogInformation("Clicando no botão '{ButtonName}'", SkipMfaButtonName);
        await skipMfaButton.ClickAsync(cancellationToken);
        logger.LogInformation("Clique em 'Pular por enquanto' concluído");

        logger.LogInformation("Aguardando o botão 'Pular por enquanto' desaparecer");
        await skipMfaButton.WaitForHiddenAsync(cancellationToken);
        logger.LogInformation("Botão 'Pular por enquanto' não está mais visível");

        logger.LogInformation("Aguardando NetworkIdle após ignorar MFA");
        await page.WaitForLoadAsync(cancellationToken);
        logger.LogInformation("Tela de MFA ignorada");
    }

    private async Task CaptureMfaScreenshotAsync(IAutomationPage page)
    {
        logger.LogInformation(
            "Salvando screenshot da tela MFA antes do clique em {ScreenshotPath}",
            MfaScreenshotPath);

        try
        {
            await page.ScreenshotAsync(MfaScreenshotPath, CancellationToken.None);
            logger.LogInformation("Screenshot anterior ao clique de MFA salva em {ScreenshotPath}", MfaScreenshotPath);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Não foi possível salvar a screenshot anterior ao clique de MFA. Exceção completa: {ExceptionDetails}",
                exception.ToString());
        }
    }

    private async Task RegisterFailureDiagnosticsAsync(IAutomationPage page)
    {
        logger.LogError("URL atual no momento da falha: {CurrentUrl}", page.Url);

        try
        {
            var title = await page.GetTitleAsync(CancellationToken.None);
            logger.LogError("Título da página no momento da falha: {PageTitle}", title);
        }
        catch (Exception titleException)
        {
            logger.LogError(
                titleException,
                "Não foi possível obter o título da página. Exceção completa: {ExceptionDetails}",
                titleException.ToString());
        }

        var screenshotPath = Path.Combine(
            "logs",
            "screenshots",
            $"authentication-failure-{DateTimeOffset.Now:yyyyMMdd-HHmmssfff}.png");

        try
        {
            await page.ScreenshotAsync(screenshotPath, CancellationToken.None);
            logger.LogError("Screenshot da falha salva em {ScreenshotPath}", screenshotPath);
        }
        catch (Exception screenshotException)
        {
            logger.LogError(
                screenshotException,
                "Não foi possível salvar a screenshot. Exceção completa: {ExceptionDetails}",
                screenshotException.ToString());
        }
    }
}
