using System.Net;

namespace AvaBot.Tests;

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    public int RequestCount { get; private set; }

    public string? LastRequestBody { get; private set; }

    public void EnqueueJson(HttpStatusCode statusCode, string json) =>
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("Nenhuma resposta HTTP simulada foi configurada.");
        }

        return _responses.Dequeue()(request);
    }
}
