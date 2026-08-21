using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvaBot.Domain.Interfaces;
using AvaBot.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvaBot.Infrastructure.Persistence;

public sealed class ActivityProgressStore : IActivityProgressStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<ActivityProgressStore> _logger;
    private readonly string _statePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HashSet<string>? _completedActivities;

    public ActivityProgressStore(
        IOptions<PortalOptions> portalOptions,
        ILogger<ActivityProgressStore> logger)
        : this(BuildScopedStatePath(portalOptions.Value.Login), logger)
    {
    }

    internal static string BuildScopedStatePath(string accountLogin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountLogin);
        var normalizedLogin = accountLogin.Trim();
        var accountHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedLogin)))
            .ToLowerInvariant()[..16];

        return Path.Combine(
            "logs",
            "state",
            accountHash,
            "activity-progress.json");
    }

    public ActivityProgressStore(
        string statePath,
        ILogger<ActivityProgressStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        _statePath = Path.GetFullPath(statePath);
        _logger = logger;
    }

    public async Task<bool> IsCompletedAsync(
        string activityIdentifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityIdentifier);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _completedActivities!.Contains(activityIdentifier.Trim());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkCompletedAsync(
        string activityIdentifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityIdentifier);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            await EnsureLoadedAsync(cancellationToken);

            if (!_completedActivities!.Add(activityIdentifier.Trim()))
            {
                return;
            }

            var directory = Path.GetDirectoryName(_statePath)!;
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(
                _completedActivities.OrderBy(identifier => identifier),
                SerializerOptions);
            await File.WriteAllTextAsync(
                _statePath,
                json,
                cancellationToken);

            _logger.LogInformation(
                "Conclusão local da atividade registrada em {StatePath}",
                _statePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_completedActivities is not null)
        {
            return;
        }

        if (!File.Exists(_statePath))
        {
            _completedActivities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_statePath, cancellationToken);
            var entries = JsonSerializer.Deserialize<string[]>(json, SerializerOptions) ?? [];
            _completedActivities = new HashSet<string>(
                entries,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"O arquivo de progresso '{_statePath}' contém JSON inválido.",
                exception);
        }
    }
}
