using System.Collections.Concurrent;

namespace Jellyfin.Plugin.PauseTrackId.Services;

/// <summary>
/// Stores one-shot recognition results for the web client.
/// </summary>
public sealed class RecognitionResultStore
{
    private readonly ConcurrentDictionary<string, PendingRecognitionResult> _pendingByDeviceId = new(StringComparer.Ordinal);

    public void Publish(string? deviceId, string displayText, double score, string? itemId, int autoHideSeconds)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(displayText))
        {
            return;
        }

        var ttl = TimeSpan.FromSeconds(Math.Max(10, autoHideSeconds * 3));
        var now = DateTimeOffset.UtcNow;

        _pendingByDeviceId[deviceId] = new PendingRecognitionResult(
            displayText,
            score,
            itemId,
            Math.Max(3, autoHideSeconds),
            now,
            now.Add(ttl));
    }

    public PendingRecognitionResult? TakeNextForDevice(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        if (!_pendingByDeviceId.TryRemove(deviceId, out var result))
        {
            return null;
        }

        if (result.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return result;
    }
}

/// <summary>
/// A pending recognition result waiting for the web client.
/// </summary>
public sealed record PendingRecognitionResult(
    string DisplayText,
    double Score,
    string? ItemId,
    int AutoHideSeconds,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset ExpiresAtUtc);
