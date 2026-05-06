namespace Jellyfin.Plugin.PauseTrackId.Helper;

/// <summary>
/// Request payload used by the File Transformation plugin callback.
/// </summary>
public sealed class PayloadRequest
{
    public string? Contents { get; set; }
}
