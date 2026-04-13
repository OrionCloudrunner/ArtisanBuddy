# ArtisanBuddy

ArtisanBuddy is a small Dalamud plugin that coordinates **GatherBuddy Reborn** and **Artisan**.

Its job is simple: let Artisan craft during real GatherBuddy downtime, then get out of the way the moment GatherBuddy has meaningful work again.

## Why this exists

Timed gathering windows create an awkward gap between the two upstream plugins:

- **GatherBuddy Reborn** knows when a timed node matters, but it can sit idle between windows.
- **Artisan** can keep a crafting list running, but it normally has no idea when gathering should preempt crafting.

Without a coordinator, the character can end up:

- standing idle during GatherBuddy dead time instead of crafting, or
- stuck crafting while a timed gathering window opens.

ArtisanBuddy fills that gap with a narrow arbitration layer over existing IPC exposed by both plugins.

## Features

- Pauses **Artisan crafting lists** when **GatherBuddy Reborn** has real work again.
- Resumes Artisan only during GatherBuddy's true idle state: `No available items to gather`.
- Optional **apartment-only** crafting resume mode.
- Conservative **teleport stall recovery** if GatherBuddy gets stuck after a swallowed teleport attempt.
- Preserves Artisan list progress by using `Artisan.SetStopRequest(true/false)` instead of restarting the list from the beginning.
- Simple status/settings window via `/artisanbuddy`.

## Intended scope

ArtisanBuddy is intentionally narrow.

It is designed around this workflow:

- **GatherBuddy Reborn** auto-gather
- **Artisan crafting lists**
- switching between timed gathering windows and list crafting

It is **not** a general automation orchestrator for every plugin or every Artisan mode.

## How it works

ArtisanBuddy polls both plugins over Dalamud IPC.

### Crafting is allowed only when all of these are true

- GatherBuddy Reborn IPC is available
- auto-gather is enabled
- GatherBuddy reports `waiting == true`
- GatherBuddy status text is exactly `No available items to gather`
- if apartment-only mode is enabled, the player is inside their own apartment

### Artisan is paused for everything else

That includes states such as:

- `Teleporting...`
- movement to a node
- active gathering flow
- non-idle GatherBuddy states

### Teleport recovery

If GatherBuddy appears stuck in a teleport state, ArtisanBuddy can temporarily bounce GatherBuddy's auto-gather state to recover.

This recovery is intentionally conservative:

- it does **not** retry while the player is still in states where teleporting is not possible
- it tries to avoid creating repeated command spam during crafting handoff

## Configuration

Open the main window with:

- `/artisanbuddy`

Current settings include:

- **Enable plugin**
- **Only resume crafting in own apartment**
- **Manage Artisan lists only**
- **Ignore GBR `Player is busy...` while Artisan is crafting**
- **Enable teleport recovery**
- poll interval
- resume delay
- teleport stall timeout
- verbose logging

## Requirements

- Windows
- XIVLauncher / Dalamud
- **GatherBuddy Reborn**
- **Artisan**
- .NET SDK compatible with **Dalamud API 14** / `Dalamud.NET.Sdk/14.0.2` for local builds

## Installation

### Option 1: custom Dalamud repository

Add this repository URL in Dalamud custom plugin repositories:

- `https://raw.githubusercontent.com/OrionCloudrunner/ArtisanBuddy/main/repo.json`

After adding the repository, install **ArtisanBuddy** from Dalamud and keep it updated there.

### Option 2: local dev plugin

Build the project and register the DLL as a Dalamud dev plugin, for example:

- `...\ArtisanBuddy\ArtisanBuddy\bin\x64\Debug\ArtisanBuddy.dll`

Then enable it in Dalamud dev plugins.

## Build

### Build output

This project is pinned to an x64 output path so the plugin lands in:

- `ArtisanBuddy/bin/x64/Debug/ArtisanBuddy.dll`
- `ArtisanBuddy/bin/x64/Release/ArtisanBuddy.dll`

### Example build

```powershell
dotnet restore .\ArtisanBuddy.sln
dotnet build .\ArtisanBuddy.sln -c Debug
```

## Known limitations

- The arbitration logic depends partly on **GatherBuddy Reborn status text**. If upstream wording changes, ArtisanBuddy may need updates.
- This is designed around **Artisan crafting lists**. Other Artisan modes may not behave as cleanly.
- It has been tested against a real personal workflow, but it should still be treated as an early public release rather than a forever-stable automation platform.

## Troubleshooting

### Artisan keeps crafting when a node opens

Check whether GatherBuddy's reported status is still in a genuine idle state. If the upstream plugin changes its status strings, ArtisanBuddy may need an update.

### GatherBuddy says `Player is busy...` repeatedly

That can be normal while Artisan owns crafting. ArtisanBuddy can be configured to ignore that specific noise while crafting is still in progress.

### Teleport recovery feels too aggressive or too conservative

Adjust:

- resume delay
- teleport stall timeout
- teleport recovery enable/disable

## Development notes

This plugin relies on public IPC exposed by:

- GatherBuddy Reborn
- Artisan

It does **not** patch or replace either plugin. It only coordinates them.

## Release process

`dist/ArtisanBuddy.zip` is the published artifact consumed by Dalamud via `repo.json`.

1. Update the version in `ArtisanBuddy/ArtisanBuddy.csproj`.
2. Build the plugin locally on a machine with a working Dalamud development environment.
3. Replace `dist/ArtisanBuddy.zip` with a fresh zip containing:
   - `ArtisanBuddy.dll`
   - `ArtisanBuddy.json`
   - `ArtisanBuddy.deps.json`
4. Update `repo.json` so `AssemblyVersion` and `LastUpdate` match the release.
5. Commit the source changes and the updated `dist/ArtisanBuddy.zip` together.
6. Create and push a tag such as `v0.1.0`.
7. The tag automatically creates or updates a GitHub Release and uploads `dist/ArtisanBuddy.zip` as the release asset.
8. Dalamud clients pointed at `repo.json` can update normally.

## Public release checklist

- build fresh from current source
- confirm the generated `ArtisanBuddy.json` beside the DLL matches current metadata
- replace `dist/ArtisanBuddy.zip` with the newly built artifact
- verify `/artisanbuddy` opens the main window
- verify the apartment-only toggle is visible and working
- test one full cycle:
  - GBR idle -> Artisan resumes
  - timed node opens -> Artisan pauses
  - GatherBuddy teleports/moves/gathers successfully
- confirm disabling/unloading ArtisanBuddy does not leave GatherBuddy disabled
- confirm `repo.json` still points at `dist/ArtisanBuddy.zip`
- confirm the raw `repo.json` URL works in Dalamud

## License

MIT

## Disclaimer

ArtisanBuddy is an unofficial community plugin. It is not affiliated with Square Enix, Goatcorp, GatherBuddy Reborn, or Artisan.
