using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Application.Services;

public sealed class AvaAuthenticationService(
    IAvaLoginService avaLoginService,
    IOptions<PortalOptions> portalOptions,
    IOptions<AvaOptions> avaOptions,
    ILogger<AvaAuthenticationService> logger) : IAvaAuthenticationService
{
    private const string LoginSelector = "#username";
    private const string ManualLoginUrlFragment = "loginmanualunidade.php";

    private readonly PortalOptions _portalOptions = portalOptions.Value;
    private readonly AvaOptions _avaOptions = avaOptions.Value;

    public async Task AuthenticateAsync(
        IAutomationPage avaPage,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Iniciando verificação de autenticação do AVA");
        logger.LogInformation("URL atual do AVA: {CurrentUrl}", avaPage.Url);

        var isManualLoginUrl = avaPage.Url.Contains(
            ManualLoginUrlFragment,
            StringComparison.OrdinalIgnoreCase);
        logger.LogInformation("URL contém página de login manual: {IsManualLoginUrl}", isManualLoginUrl);

        var loginFields = avaPage.GetLocator(LoginSelector);
        var loginFieldCount = await loginFields.CountAsync(cancellationToken);
        logger.LogInformation("Quantidade de campos de login encontrados: {LoginFieldCount}", loginFieldCount);

        if (loginFieldCount == 0)
        {
            logger.LogInformation("AVA autenticado automaticamente via Portal.");
            return;
        }

        var credentials = ResolveCredentials();
        logger.LogInformation(
            "Tela de login do AVA encontrada; origem do login: {LoginSource}; origem da senha: {PasswordSource}",
            credentials.LoginSource,
            credentials.PasswordSource);

        await avaLoginService.LoginAsync(
            avaPage,
            credentials.Login,
            credentials.Senha,
            cancellationToken);

        logger.LogInformation("Autenticação manual do AVA concluída");
    }

    private ResolvedCredentials ResolveCredentials()
    {
        var usePortalLogin = string.IsNullOrWhiteSpace(_avaOptions.Login);
        var usePortalPassword = string.IsNullOrWhiteSpace(_avaOptions.Senha);

        var login = usePortalLogin ? _portalOptions.Login : _avaOptions.Login;
        var senha = usePortalPassword ? _portalOptions.Senha : _avaOptions.Senha;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
        {
            throw new InvalidOperationException(
                "Não existem credenciais válidas para autenticar no AVA. Verifique Portal e Ava no appsettings.json.");
        }

        return new ResolvedCredentials(
            login,
            senha,
            usePortalLogin ? "Portal" : "Ava",
            usePortalPassword ? "Portal" : "Ava");
    }

    private sealed record ResolvedCredentials(
        string Login,
        string Senha,
        string LoginSource,
        string PasswordSource);
}
