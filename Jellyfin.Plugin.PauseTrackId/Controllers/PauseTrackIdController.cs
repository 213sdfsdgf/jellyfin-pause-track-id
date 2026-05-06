using System.Text;
using Jellyfin.Plugin.PauseTrackId.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PauseTrackId.Controllers;

/// <summary>
/// Web and API endpoints for the Pause Track ID plugin.
/// </summary>
[ApiController]
public sealed class PauseTrackIdController(RecognitionResultStore recognitionResultStore) : ControllerBase
{
    private const string WebClientResourcePath = "Jellyfin.Plugin.PauseTrackId.Configuration.pause-track-id-web.js";
    private readonly RecognitionResultStore _recognitionResultStore = recognitionResultStore;

    /// <summary>
    /// Returns the embedded web client script.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("PauseTrackId/Web/client.js")]
    public IActionResult GetWebClientScript()
    {
        var assembly = typeof(Plugin).Assembly;
        using var stream = assembly.GetManifestResourceStream(WebClientResourcePath);
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Content(reader.ReadToEnd(), "application/javascript; charset=utf-8");
    }

    /// <summary>
    /// Returns the next pending recognition result for the given device.
    /// </summary>
    [Authorize]
    [HttpGet("PauseTrackId/Active")]
    [ProducesResponseType(typeof(PendingRecognitionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<PendingRecognitionResponse> GetActive([FromQuery] string deviceId)
    {
        var result = _recognitionResultStore.TakeNextForDevice(deviceId);
        if (result is null)
        {
            return NoContent();
        }

        return new PendingRecognitionResponse(
            result.DisplayText,
            result.Score,
            result.ItemId,
            result.AutoHideSeconds,
            result.PublishedAtUtc);
    }
}

/// <summary>
/// Response payload returned to the Jellyfin web client.
/// </summary>
public sealed record PendingRecognitionResponse(
    string DisplayText,
    double Score,
    string? ItemId,
    int AutoHideSeconds,
    DateTimeOffset PublishedAtUtc);
