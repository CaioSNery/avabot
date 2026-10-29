using System.Reflection;
using AvaBot.Application.Services;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace AvaBot.Tests;

public sealed class ActivityNavigationNetworkIdleTests
{
    [Theory]
    [InlineData(ActivityType.WebAula)]
    [InlineData(ActivityType.VideoAulaResumo)]
    [InlineData(ActivityType.ConteudoProfessor)]
    [InlineData(ActivityType.MaterialInformativo)]
    [InlineData(ActivityType.AulaPratica)]
    public async Task PassiveContent_NetworkTimeoutDoesNotInterruptFlow(ActivityType type)
    {
        await WaitAsync(type, new TimeoutException());
    }

    [Theory]
    [InlineData(ActivityType.AtividadeDiagnostica)]
    [InlineData(ActivityType.AtividadeAprendizagem)]
    [InlineData(ActivityType.AvaliacaoUnidade)]
    [InlineData(ActivityType.Questionario)]
    public async Task Quiz_NetworkTimeoutRemainsMandatory(ActivityType type)
    {
        await Assert.ThrowsAsync<TimeoutException>(() => WaitAsync(type, new TimeoutException()));
    }

    [Fact]
    public async Task ClosedPage_IsNotIgnored()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WaitAsync(ActivityType.ConteudoProfessor, new InvalidOperationException("Page closed")));
    }

    [Fact]
    public async Task Cancellation_IsNotIgnored()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            WaitAsync(ActivityType.ConteudoProfessor, new OperationCanceledException()));
    }

    [Fact]
    public async Task TimeoutDuringCancellation_IsNotIgnored()
    {
        await Assert.ThrowsAsync<TimeoutException>(() =>
            WaitAsync(ActivityType.ConteudoProfessor, new TimeoutException(), new CancellationToken(true)));
    }

    private static Task WaitAsync(ActivityType type, Exception failure, CancellationToken token = default)
    {
        var page = DispatchProxy.Create<IAutomationPage, NetworkFailurePage>();
        ((NetworkFailurePage)(object)page).Failure = failure;
        var service = new ActivityNavigationService(null!, NullLogger<ActivityNavigationService>.Instance);
        var method = typeof(ActivityNavigationService).GetMethod(
            "WaitForNetworkIdleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)method.Invoke(service,
            [page, new Activity { Nome = "Conteúdo", Tipo = type, Locator = null! }, "durante a abertura", token])!;
    }

    public class NetworkFailurePage : DispatchProxy
    {
        public Exception Failure { get; set; } = new TimeoutException();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IAutomationPage.WaitForNetworkIdleAsync))
                return Task.FromException(Failure);

            throw new NotSupportedException(targetMethod?.Name);
        }
    }
}
