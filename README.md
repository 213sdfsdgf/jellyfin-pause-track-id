# Jellyfin Pause Track ID

Jellyfin server plugin that listens for a **pause** event, takes the **last 5 seconds** of the currently playing local media file, generates a Chromaprint fingerprint **directly with Jellyfin's configured FFmpeg**, looks it up via **AcoustID**, and sends the result back to the active session as an on-screen **DisplayMessage** popup.

## What it does

- subscribes to `ISessionManager.PlaybackProgress`
- triggers only on the **edge** `playing -> paused`
- uses Jellyfin's own resolved `IMediaEncoder.EncoderPath` instead of a plugin-specific ffmpeg path
- generates a Chromaprint fingerprint with FFmpeg's built-in `chromaprint` muxer
- queries `https://api.acoustid.org/v2/lookup`
- sends `Artist — Title` back to the current Jellyfin session

## Important limitation

This plugin uses Jellyfin's built-in **DisplayMessage** session command. That means it can show a popup/message on supported clients, but it is **not** a fully custom graphical overlay system for every Jellyfin client.

If you want a richer overlay inside the **web client**, the next step is a companion `jellyfin-web` frontend extension that renders a custom toast/modal when the server plugin emits the match.

## Why this version is better

The plugin no longer asks the user to configure paths for `ffmpeg` or `fpcalc`.

- `ffmpeg` is taken from Jellyfin itself via `IMediaEncoder.EncoderPath`
- `fpcalc` is no longer required at all

So if Jellyfin playback/transcoding already works and the bundled/system FFmpeg has the `chromaprint` muxer enabled, the plugin can work immediately after install once you set the AcoustID API key.

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
3. Add a `jellyfin-web` companion plugin for a prettier on-screen overlay.
4. Support video files too by analyzing the paused media's audio stream even when `RestrictToAudioItems` is disabled.
