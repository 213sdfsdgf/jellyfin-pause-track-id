# Jellyfin Pause Track ID

Jellyfin server plugin that listens for a **pause** event, takes the **last 5 seconds** of the currently playing local media file, generates a Chromaprint fingerprint **directly with Jellyfin's configured FFmpeg**, looks it up via **AcoustID**, and exposes the result to Jellyfin Web as a dedicated clickable button.

## What it does

- subscribes to `ISessionManager.PlaybackProgress`
- triggers only on the **edge** `playing -> paused`
- uses Jellyfin's own resolved `IMediaEncoder.EncoderPath` instead of a plugin-specific ffmpeg path
- generates a Chromaprint fingerprint with FFmpeg's built-in `chromaprint` muxer
- queries `https://api.acoustid.org/v2/lookup`
- publishes `Artist — Title` to a dedicated Jellyfin Web button
- can optionally fall back to Jellyfin's built-in `DisplayMessage` popup

## Important limitation

The preferred UI now targets **Jellyfin Web** through an isolated injected client script. The built-in `DisplayMessage` popup remains available as an optional fallback for other clients.

The web button follows the same general integration pattern used by Intro Skipper, but stays isolated from Intro Skipper's own DOM/classes/logic so the two interfaces do not conflict.

## Why this version is better

The plugin no longer asks the user to configure paths for `ffmpeg` or `fpcalc`.

- `ffmpeg` is taken from Jellyfin itself via `IMediaEncoder.EncoderPath`
- `fpcalc` is no longer required at all

So if Jellyfin playback/transcoding already works and the bundled/system FFmpeg has the `chromaprint` muxer enabled, the plugin can work immediately after install once you set the AcoustID API key.

## Web button integration

To show the recognized track as a clickable button in Jellyfin Web:

- install the **File Transformation** plugin
- keep **Show a web-player button when a track is recognized** enabled in Pause Track ID settings
- optionally leave **legacy popup fallback** disabled to avoid duplicate UI

The injected web UI is deliberately isolated:

- its DOM and CSS are namespaced under `pause-track-id-*`
- it injects its loader only once
- it does not patch Intro Skipper's button logic
- it renders in the top-right corner to avoid the usual skip-intro button area

## Files

- `Jellyfin.Plugin.PauseTrackId/Plugin.cs`
- `Jellyfin.Plugin.PauseTrackId/PluginServiceRegistrator.cs`
- `Jellyfin.Plugin.PauseTrackId/Configuration/PluginConfiguration.cs`
- `Jellyfin.Plugin.PauseTrackId/Configuration/configPage.html`
- `Jellyfin.Plugin.PauseTrackId/Services/PauseRecognitionHostedService.cs`
- `Jellyfin.Plugin.PauseTrackId/Services/ChromaprintRecognitionService.cs`

## Build prerequisites

- .NET SDK 9
- Jellyfin-compatible package versions matching your target server version
- Jellyfin with a working FFmpeg binary
- FFmpeg built with the `chromaprint` muxer
- an AcoustID API key

## Build

```bash
cd Jellyfin.Plugin.PauseTrackId
dotnet restore
dotnet build -c Release
```

## Install into Jellyfin

Copy the built plugin output into a dedicated folder inside Jellyfin's plugins directory, for example:

```bash
mkdir -p /var/lib/jellyfin/plugins/PauseTrackId
cp bin/Release/net9.0/* /var/lib/jellyfin/plugins/PauseTrackId/
```

Then restart Jellyfin.

## Runtime requirements on the Jellyfin server

You no longer need to install `fpcalc` separately.

You only need Jellyfin's FFmpeg to be available and to support Chromaprint. On a host shell you can verify that with:

```bash
ffmpeg -hide_banner -formats | grep chromaprint
```

Expected output contains something like:

```text
E  chromaprint     Chromaprint
```

## Suggested next improvements

1. Add a small cache by `PlaySessionId + rounded position` to avoid repeated lookups.
2. Add a manual REST endpoint like `/Plugins/PauseTrackId/Recognize` for testing.
3. Expose richer actions on the button itself, such as opening a search or copying more metadata.
4. Support video files too by analyzing the paused media's audio stream even when `RestrictToAudioItems` is disabled.
