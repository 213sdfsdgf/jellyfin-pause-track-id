# Jellyfin Pause Track ID

Jellyfin server plugin that listens for a **pause** event, extracts the **last 5 seconds** of the currently playing local media file, fingerprints the audio with **Chromaprint (`fpcalc`)**, looks it up via **AcoustID**, and sends the result back to the active session as an on-screen **DisplayMessage** popup.

## What it does

- subscribes to `ISessionManager.PlaybackProgress`
- triggers only on the **edge** `playing -> paused`
- extracts a short mono WAV clip with `ffmpeg`
- computes a Chromaprint fingerprint with `fpcalc -json`
- queries `https://api.acoustid.org/v2/lookup`
- sends `Artist — Title` back to the current Jellyfin session

## Important limitation

This plugin uses Jellyfin's built-in **DisplayMessage** session command. That means it can show a popup/message on supported clients, but it is **not** a fully custom graphical overlay system for every Jellyfin client.

If you want a richer overlay inside the **web client**, the next step is a companion `jellyfin-web` frontend extension that renders a custom toast/modal when the server plugin emits the match.

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
- `ffmpeg` on the Jellyfin server
- `fpcalc` on the Jellyfin server (`chromaprint-tools` on Debian/Ubuntu)
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

## Server-side packages on Debian/Ubuntu

```bash
apt update
apt install -y ffmpeg libchromaprint-tools
```

If `libchromaprint-tools` is unavailable in your repo, install the package that provides `fpcalc` on your distro and verify with:

```bash
which ffmpeg
which fpcalc
```

## Suggested next improvements

1. Add a small cache by `PlaySessionId + rounded position` to avoid repeated lookups.
2. Add a manual REST endpoint like `/Plugins/PauseTrackId/Recognize` for testing.
3. Add a `jellyfin-web` companion plugin for a prettier on-screen overlay.
4. Support video files too by analyzing the paused media's audio stream even when `RestrictToAudioItems` is disabled.
