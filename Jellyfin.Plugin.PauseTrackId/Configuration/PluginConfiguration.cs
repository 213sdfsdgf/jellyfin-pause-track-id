using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.PauseTrackId.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    public bool Enabled { get; set; } = true;

    public bool EnableWebButton { get; set; } = false;

    public bool ShowDisplayMessageFallback { get; set; } = false;

    public int WebButtonHideSeconds { get; set; } = 12;

    public string AcoustIdApiKey { get; set; } = string.Empty;

    public bool RestrictToAudioItems { get; set; } = true;

    public int AnalysisWindowSeconds { get; set; } = 5;

    public int RecognitionTimeoutSeconds { get; set; } = 20;

    public int MessageTimeoutMs { get; set; } = 10000;

    public double MinimumScore { get; set; } = 0.60d;
}
