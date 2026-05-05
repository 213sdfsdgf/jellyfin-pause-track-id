# Jellyfin plugin repository for Pause Track ID

This folder contains the static files needed so Jellyfin can install the plugin through:

`Dashboard -> Plugins -> Repositories`

## Files

- `manifest.json` — repository manifest consumed by Jellyfin
- `pause-track-id_<version>.zip` — release archive containing the plugin binaries

## How Jellyfin uses it

1. You host `manifest.json` at a public URL.
2. Inside the manifest, `sourceUrl` points to the downloadable plugin zip.
3. In Jellyfin, add the manifest URL under `Plugins -> Repositories`.
4. The plugin appears in the catalog and can be installed from the UI.

## Recommended hosting options

- GitHub Releases for the `.zip`
- GitHub Pages / raw static hosting / nginx / Caddy for `manifest.json`

## Important

The current `manifest.json` in this repo is a **template**.
Before using it, replace:

- `sourceUrl`
- `checksum`
- `timestamp`
- `owner` if desired

## Generate real release artifacts

Use:

```bash
./scripts/build-plugin-repository.sh <public_zip_url> <target_abi> [version]
```

Example:

```bash
./scripts/build-plugin-repository.sh \
  https://github.com/USER/REPO/releases/download/v1.0.0.0/pause-track-id_1.0.0.0.zip \
  10.11.0.0 \
  1.0.0.0
```

This script will:

- run `dotnet publish`
- zip the plugin output
- compute the MD5 checksum Jellyfin expects in the repository manifest
- emit a ready-to-host `artifacts/repository/manifest.json`

## Install prerequisites on the Jellyfin server

The plugin itself still needs these runtime tools on the Jellyfin host:

```bash
apt update
apt install -y ffmpeg libchromaprint-tools
```

And you must set your AcoustID API key in the plugin config page after installation.
