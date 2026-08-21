namespace AvaBot.Domain.Interfaces;

public interface IBrowserSession : IAsyncDisposable
{
    IAutomationPage Page { get; }
}
