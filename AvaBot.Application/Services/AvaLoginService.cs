using AvaBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AvaBot.Application.Services;

public sealed class AvaLoginService(ILogger<AvaLoginService> logger) : IAvaLoginService
{
    private const string ManualLoginUrlFragment = "loginmanualunidade.php";
    private const string LoginSelector = "#username";
    private const string PasswordSelector = "input[name='password']";
    private const string EnterButtonName = "Entrar";
    private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(15);

    public async Task LoginAsync(
        IAutomationPage avaPage,
        string login,
        string senha,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(senha);

        var isManualLoginUrl = avaPage.Url.Contains(
            ManualLoginUrlFragment,
            StringComparison.OrdinalIgnoreCase);

        logger.LogInformation(
            "URL de login manual identificada: {IsManualLoginUrl}. URL atual: {CurrentUrl}",
            isManualLoginUrl,
            avaPage.Url);

        logger.LogInformation("Aguardando campo de login do AVA");
        await avaPage.WaitForVisibleAsync(LoginSelector, cancellationToken);
        await avaPage.FillAsync(LoginSelector, login, cancellationToken);
        logger.LogInformation("Login preenchido");

        logger.LogInformation("Aguardando campo de senha do AVA");
        await avaPage.WaitForVisibleAsync(PasswordSelector, cancellationToken);
        await avaPage.FillAsync(PasswordSelector, senha, cancellationToken);
        logger.LogInformation("Senha preenchida");

        var enterButton = avaPage.GetButtonByRole(EnterButtonName);
        var buttonIsVisible = await enterButton.TryWaitForVisibleAsync(
            ControlTimeout,
            cancellationToken);
        var buttonCount = await enterButton.CountAsync(cancellationToken);

        logger.LogInformation(
            "Botão Entrar encontrado: {ButtonCount}; visível: {IsVisible}",
            buttonCount,
            buttonIsVisible);

        if (buttonCount == 0 || !buttonIsVisible)
        {
            throw new InvalidOperationException("O botão 'Entrar' do AVA não foi encontrado ou não está visível.");
        }

        await enterButton.ClickAsync(cancellationToken);
        logger.LogInformation("Clique em Entrar realizado");

        await avaPage.WaitForDomContentLoadedAsync(cancellationToken);
        await avaPage.WaitForNetworkIdleAsync(cancellationToken);

        var title = await avaPage.GetTitleAsync(cancellationToken);
        logger.LogInformation("URL final: {FinalUrl}", avaPage.Url);
        logger.LogInformation("Título da página: {PageTitle}", title);
    }
}
