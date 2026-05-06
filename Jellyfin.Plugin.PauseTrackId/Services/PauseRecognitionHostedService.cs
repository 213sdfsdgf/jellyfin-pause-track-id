using System.Collections.Concurrent;
using Jellyfin.Plugin.PauseTrackId.Configuration;
using Jellyfin.Plugin.PauseTrackId.Helper;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PauseTrackId.Services;

/// <summary>
/// Listens for pause transitions and runs recognition once per pause edge.
/// </summary>
public sealed class PauseRecognitionHostedService : IHostedService, IDisposable
{
    private readonly object _stateLock = new();
    private readonly Dictionary<string, bool> _pauseState = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _activeRecognitions = new(StringComparer.Ordinal);
    private readonly ISessionManager _sessionManager;
    private readonly ChromaprintRecognitionService _recognitionService;
    private readonly RecognitionResultStore _recognitionResultStore;
    private readonly ILogger<PauseRecognitionHostedService> _logger;

    public PauseRecognitionHostedService(
        ISessionManager sessionManager,
        ChromaprintRecognitionService recognitionService,
        RecognitionResultStore recognitionResultStore,
        ILogger<PauseRecognitionHostedService> logger)
    {
        _sessionManager = sessionManager;
        _recognitionService = recognitionService;
        _recognitionResultStore = recognitionResultStore;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;
        if (config is not null)
        {
            try
            {
                WebInjector.TryRegister(config, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pause Track ID failed to initialize the optional web-button injector. Jellyfin startup will continue without it.");
            }
        }

        _sessionManager.PlaybackProgress += OnPlaybackProgress;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
    }

    private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs eventArgs)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!config.Enabled)
        {
            return;
        }

        var sessionKey = string.IsNullOrWhiteSpace(eventArgs.PlaySessionId)
            ? eventArgs.Session?.Id
            : eventArgs.PlaySessionId;

        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            return;
        }

        var shouldTrigger = false;
        lock (_stateLock)
        {
            var wasPaused = _pauseState.TryGetValue(sessionKey, out var previousPaused) && previousPaused;
            _pauseState[sessionKey] = eventArgs.IsPaused;

            if (!eventArgs.IsPaused)
            {
                return;
            }

            shouldTrigger = !wasPaused;
        }

        if (!shouldTrigger)
        {
            return;
        }

        if (!_activeRecognitions.TryAdd(sessionKey, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var match = await _recognitionService.TryRecognizeAsync(eventArgs, CancellationToken.None).ConfigureAwait(false);
                if (match is null || eventArgs.Session is null)
                {
                    return;
                }

                _recognitionResultStore.Publish(
                    eventArgs.Session.DeviceId,
                    match.DisplayText,
                    match.Score,
                    eventArgs.Item?.Id.ToString(),
                    config.WebButtonHideSeconds);

                if (config.ShowDisplayMessageFallback)
                {
                    await _sessionManager.SendMessageCommand(
                        string.Empty,
                        eventArgs.Session.Id,
                        new MessageCommand
                        {
                            Header = "Track recognized",
                            Text = match.DisplayText,
                            TimeoutMs = config.MessageTimeoutMs
                        },
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pause-time track recognition failed.");
            }
            finally
            {
                _activeRecognitions.TryRemove(sessionKey, out _);
            }
        });
    }
}
