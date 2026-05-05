using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Jellyfin.Plugin.PauseTrackId.Configuration;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PauseTrackId.Services;

/// <summary>
/// Runs Chromaprint locally and AcoustID remotely.
/// </summary>
public sealed class ChromaprintRecognitionService
{
    private const string LookupEndpoint = "https://api.acoustid.org/v2/lookup";
    private readonly HttpClient _httpClient = new();
    private readonly ILogger<ChromaprintRecognitionService> _logger;

    public ChromaprintRecognitionService(ILogger<ChromaprintRecognitionService> logger)
    {
        _logger = logger;
    }

    public async Task<TrackMatchResult?> TryRecognizeAsync(PlaybackProgressEventArgs eventArgs, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance;
        var config = plugin?.Configuration;
        if (plugin is null || config is null || !config.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.AcoustIdApiKey))
        {
            _logger.LogWarning("Pause Track ID skipped because AcoustID API key is not configured.");
            return null;
        }

        if (eventArgs.Item is null || string.IsNullOrWhiteSpace(eventArgs.Item.Path))
        {
            _logger.LogDebug("Pause Track ID skipped because the playing item has no local file path.");
            return null;
        }

        if (config.RestrictToAudioItems && eventArgs.Item is not Audio)
        {
            return null;
        }

        var filePath = eventArgs.Item.Path;
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Pause Track ID skipped because media file does not exist: {Path}", filePath);
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.RecognitionTimeoutSeconds));
        var effectiveCancellationToken = timeoutCts.Token;

        var workingDirectory = Path.Combine(plugin.DataFolderPath, "pause-track-id-temp");
        Directory.CreateDirectory(workingDirectory);

        var clipPath = Path.Combine(workingDirectory, $"clip-{Guid.NewGuid():N}.wav");

        try
        {
            var clipDuration = Math.Max(1, config.AnalysisWindowSeconds);
            var currentPositionSeconds = Math.Max(0d, TimeSpan.FromTicks(eventArgs.PlaybackPositionTicks ?? 0).TotalSeconds);
            var clipStartSeconds = Math.Max(0d, currentPositionSeconds - clipDuration);

            await ExtractClipAsync(config.FfmpegPath, filePath, clipPath, clipStartSeconds, clipDuration, effectiveCancellationToken).ConfigureAwait(false);
            var fingerprint = await RunFpcalcAsync(config.FpcalcPath, clipPath, effectiveCancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(fingerprint.Fingerprint) || fingerprint.Duration <= 0)
            {
                _logger.LogInformation("Pause Track ID got an empty Chromaprint fingerprint.");
                return null;
            }

            var result = await LookupAsync(config.AcoustIdApiKey, fingerprint, config.MinimumScore, effectiveCancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                _logger.LogInformation("Pause Track ID could not find a confident AcoustID match for {Path}.", filePath);
            }

            return result;
        }
        finally
        {
            TryDelete(clipPath);
        }
    }

    private async Task ExtractClipAsync(
        string ffmpegPath,
        string inputPath,
        string outputPath,
        double clipStartSeconds,
        int clipDurationSeconds,
        CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.ArgumentList.Add("-v");
        processStartInfo.ArgumentList.Add("error");
        processStartInfo.ArgumentList.Add("-ss");
        processStartInfo.ArgumentList.Add(clipStartSeconds.ToString("0.###", CultureInfo.InvariantCulture));
        processStartInfo.ArgumentList.Add("-i");
        processStartInfo.ArgumentList.Add(inputPath);
        processStartInfo.ArgumentList.Add("-t");
        processStartInfo.ArgumentList.Add(clipDurationSeconds.ToString(CultureInfo.InvariantCulture));
        processStartInfo.ArgumentList.Add("-vn");
        processStartInfo.ArgumentList.Add("-ac");
        processStartInfo.ArgumentList.Add("1");
        processStartInfo.ArgumentList.Add("-ar");
        processStartInfo.ArgumentList.Add("11025");
        processStartInfo.ArgumentList.Add("-y");
        processStartInfo.ArgumentList.Add(outputPath);

        await RunProcessAsync(processStartInfo, "ffmpeg", cancellationToken).ConfigureAwait(false);
    }

    private async Task<FingerprintResult> RunFpcalcAsync(string fpcalcPath, string clipPath, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = fpcalcPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.ArgumentList.Add("-json");
        processStartInfo.ArgumentList.Add(clipPath);

        var output = await RunProcessAsync(processStartInfo, "fpcalc", cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(output.StandardOutput);
        var root = document.RootElement;
        var fingerprint = root.TryGetProperty("fingerprint", out var fingerprintElement)
            ? fingerprintElement.GetString() ?? string.Empty
            : string.Empty;
        var duration = root.TryGetProperty("duration", out var durationElement)
            ? durationElement.GetInt32()
            : 0;

        return new FingerprintResult(fingerprint, duration);
    }

    private async Task<TrackMatchResult?> LookupAsync(
        string apiKey,
        FingerprintResult fingerprint,
        double minimumScore,
        CancellationToken cancellationToken)
    {
        using var query = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client"] = apiKey,
            ["meta"] = "recordings+releasegroups",
            ["duration"] = fingerprint.Duration.ToString(CultureInfo.InvariantCulture),
            ["fingerprint"] = fingerprint.Fingerprint
        });
        var queryString = await query.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var response = await _httpClient.GetAsync($"{LookupEndpoint}?{queryString}", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var root = document.RootElement;
        if (!root.TryGetProperty("status", out var statusElement)
            || !string.Equals(statusElement.GetString(), "ok", StringComparison.OrdinalIgnoreCase)
            || !root.TryGetProperty("results", out var resultsElement))
        {
            return null;
        }

        TrackMatchResult? bestResult = null;
        foreach (var resultElement in resultsElement.EnumerateArray())
        {
            var score = resultElement.TryGetProperty("score", out var scoreElement)
                ? scoreElement.GetDouble()
                : 0d;
            if (score < minimumScore)
            {
                continue;
            }

            if (!resultElement.TryGetProperty("recordings", out var recordingsElement))
            {
                continue;
            }

            foreach (var recordingElement in recordingsElement.EnumerateArray())
            {
                var title = recordingElement.TryGetProperty("title", out var titleElement)
                    ? titleElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var artist = TryReadArtist(recordingElement);
                var displayText = string.IsNullOrWhiteSpace(artist)
                    ? title
                    : $"{artist} — {title}";

                bestResult = new TrackMatchResult(displayText!, score);
                break;
            }

            if (bestResult is not null)
            {
                break;
            }
        }

        return bestResult;
    }

    private static string? TryReadArtist(JsonElement recordingElement)
    {
        if (!recordingElement.TryGetProperty("artists", out var artistsElement))
        {
            return null;
        }

        var artists = new List<string>();
        foreach (var artistElement in artistsElement.EnumerateArray())
        {
            if (artistElement.TryGetProperty("name", out var nameElement))
            {
                var name = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    artists.Add(name);
                }
            }
        }

        return artists.Count == 0 ? null : string.Join(", ", artists);
    }

    private static async Task<ProcessResult> RunProcessAsync(ProcessStartInfo processStartInfo, string processName, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = processStartInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {processName}.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{processName} exited with code {process.ExitCode}: {stderr}");
        }

        return new ProcessResult(stdout, stderr);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record FingerprintResult(string Fingerprint, int Duration);

    private sealed record ProcessResult(string StandardOutput, string StandardError);
}

/// <summary>
/// A recognized track match.
/// </summary>
public sealed record TrackMatchResult(string DisplayText, double Score);
