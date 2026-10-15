# Downloads page

**Date:** 2026-10-15
**Status:** design, awaiting review

## What this is

The Downloads page is the last placeholder in the **Apps** section of the sidebar. Its own
subtitle already states the intent: *"Install the apps a gaming PC needs, launchers, runtimes
and tools, in one place."*

Today it renders `WorkInProgressView` and nothing else. This design turns it into a curated
catalogue of installable apps: a grid of tiles, each with an **Install** button, backed by
`winget`.

## Why now

Installing an app is already a first-class concept in this codebase — it is an **Action**
(ADR-0004): done once, not undoable, with a warning before it starts, progress while it runs,
a log, and read-back of whether it has happened. The UWP Apps page and the Store removal path
already exercise that concept. The Downloads page is the first page whose *entire purpose* is
an Action, and it is the only one still showing a placeholder.

## What was decided (by the user, in conversation)

1. **A shipped, curated catalogue** — not a live `winget search`. Akari Dash ships a hand-picked
   list, the same way it ships its Tweak catalogue: curated, not copied. The list works without
   a network round-trip, and nothing on the page is a package we have not chosen.
2. **Tiles** (mockup option A): a grid of app cards, three across, each with an Install button.
   Not the one-row-per-app list.
3. **Confirm before installing.** Clicking Install opens a short dialog naming the app, its
   publisher, and the fact that it cannot be undone, with Install / Cancel.
4. **The app list as proposed** is good for now (~20 entries, listed below).
5. **Installs queue.** The user may click Install on several tiles; each click joins a queue, the
   page works through it in order, and each tile shows its own progress. `winget` takes a
   process-wide lock and cannot run two installs at once, so the queue makes that visible rather
   than hiding it.

## Goals

- A grid of tiles for the curated catalogue, each showing name, publisher and a one-line
  description.
- A category filter and a search box to narrow the grid.
- Each tile says whether the app is **already installed** on this machine.
- Clicking Install warns first, then installs via `winget`, with progress on that tile and a
  log of what happened; then the tile reads as installed.
- Several tiles may be queued at once; the page installs them one after another in the order
  they were clicked, and each tile shows where it is: waiting, installing, done, or failed.

## Non-goals

- **Removing** apps from this page. The UWP Apps page already owns removal (Store and desktop).
- A live search over all of `winget`, or a browsable package index.
- Version pinning, downgrades, or updating already-installed apps.
- A single "install everything" button, or installing anything the user did not click.

## Anti-goals

- Installing anything without the user asking, or without saying first that it cannot be undone.
- Showing an install as a **Tweak**. It has no Original Values and no Undo; presenting it as
  reversible would be a lie the rest of the app is built to avoid.
- Presenting a package we have not curated as if Akari Dash vouched for it.

## Constraints from the repository

- **Action semantics** (ADR-0004, `CONTEXT.md`): no Undo, a plain warning before it starts,
  progress while running, a log, and read-back of whether it has been done. A change that *can*
  be undone is never an Action; an Action never pretends to be a Tweak.
- **Content is data, not scripts** (ADR-0001). The catalogue is a declared list; the one process
  that runs is `winget`, which is what installing software is.
- **Never test on real hardware.** The user runs this in a VM. The build lands in
  `vm-build\AkariDash-Debug`, and a `vm-build\VM-CHECK-<feature>.md` documents the steps to
  follow inside the VM (the existing convention).
- **Match the neighbours.** The UWP Apps page shows the house pattern: a status line plus
  `ProgressRing` bound to `IsBusy`, a filter/search row, per-row spinners, `IInfoBarService`
  for errors, `IDialogService` for confirmation. Reuse those; do not invent a new one.
- **Tests** in `src/AkariDash.Tests` (xUnit). The catalogue is data, so it is directly testable.

## How it works

### Catalogue (Core)

A record describing one entry — the winget package id, display name, publisher, a one-line
description, and its category — plus a static, hand-written list of them, in the same spirit as
the Tweak catalogue's data files. Proposed entries, each to be verified against `winget show`
before it ships:

| Category | App | winget id (to verify) |
|---|---|---|
| Launchers | Steam | `Valve.Steam` |
| Launchers | Epic Games Launcher | `EpicGames.EpicGamesLauncher` |
| Launchers | Battle.net | `Blizzard.BattleNet` |
| Launchers | GOG Galaxy | `GOG.Galaxy` |
| Launchers | EA App | `ElectronicArts.EADesktop` |
| Launchers | Ubisoft Connect | `Ubisoft.Connect` |
| Launchers | Rockstar Games Launcher | `RockstarGames.Launcher` |
| Communication | Discord | `Discord.Discord` |
| Tools | OBS Studio | `OBSProject.OBSStudio` |
| Tools | 7-Zip | `7zip.7zip` |
| Tools | ShareX | `ShareX.ShareX` |
| Tools | Notepad++ | `Notepad++.Notepad++` |
| Tools | PowerToys | `Microsoft.PowerToys` |
| Tools | Everything (file search) | `voidtools.Everything` |
| Tools | VLC | `VideoLAN.VLC` |
| Monitoring | MSI Afterburner | `Guru3D.Afterburner` |
| Monitoring | HWiNFO | `REALiX.HWiNFO` |
| Monitoring | CPU-Z | `CPUID.CPU-Z` |
| Monitoring | GPU-Z | `TechPowerUp.GPU-Z` |
| Runtimes | Visual C++ 2015–2022 (x64) | `Microsoft.VCRedist.2015+.x64` |
| Runtimes | .NET Desktop Runtime 8 | `Microsoft.DotNet.DesktopRuntime.8` |

### Service (Core interface, App implementation)

Following `IInstalledAppService` / `WindowsStoreAppService`: an interface in
`AkariDash.Core.Apps` and a Windows implementation in `src/AkariDash.App/Services`.

- Read back whether an app is installed with `winget list --id <id> --exact --source winget`,
  which is keyed by the same package id the catalogue stores. The Uninstaller's registry signal
  (`InstalledApp.UninstallString`) cannot be used as the primary signal: it matches on
  `DisplayName`, and several entries here disagree with their registry name (installed
  `7zip.7zip` shows as "7-Zip 24.09 (x64)"). If winget is unavailable the tile shows *unknown*,
  never a false "not installed".
- Install one package by running `winget install --id <id> --source winget --exact --silent
  --accept-package-agreements --accept-source-agreements --disable-interactivity`, the exact
  flag set `WindowsStoreAppService` already uses, capturing output for the log. A non-zero exit
  is an error **except** the codes that mean the work succeeded: 3010 / 1641 (installed, restart
  required) and winget's "already installed" code. Those end as installed, the restart cases
  carrying a "restart to finish" note. Report installed state again afterwards.
- Run installs through a queue. `winget` locks globally, so the service accepts jobs one at a
  time and each job reports its own state back to its tile. Anyway: state which job is running on
  the page status line, and let the other queued tiles say they are waiting.

### Page and view model

`DownloadsPage.xaml` replaces the `WorkInProgressView` placeholder with the tile grid, the
filter chips, the search box, and the status line — modelled on `UwpAppsPage.xaml`. A new view
model supplies the tiles, the installed state, the filter/search, the confirm dialog, the
queue, the per-tile progress, and the log. Register it in `App.xaml.cs` alongside the other page
view models (`AddTransient`), and the service as `AddSingleton`.

**The log** follows the neighbours rather than adding a surface: a status line while a job runs
(`IStatusService`), a per-tile spinner, and on failure the readable reason in the info bar
(`IInfoBarService`) plus the last line of winget's output — exactly how the Store removal path
already reports. Everything is written to the existing file log (`ILogger`) as well.

### VM check

A `vm-build\VM-CHECK-DOWNLOADS.md` in the style of `VM-CHECK-UWP-APPS.md`: numbered steps for
inside the VM, including an independent way to confirm each install actually happened
(`winget list`, Add/Remove Programs, or the app's own folder).

## Calls I made (please check these)

1. **Icons are letter avatars**, not real logos — a coloured square with the app's first letter,
   as in the mockup. Shipping ~20 vendor logos means ~20 image assets and a licence question
   for each; letter tiles need none. Also considered: reading the real icon from the installed
   app's `DisplayIcon` after install (available, but you would see a letter tile before and a
   logo after).
2. **Silent installs**, reusing the existing flag set. No installer wizard appears. A handful of
   these packages ship installers that ignore `--silent`; if one behaves badly we deal with it
   per app rather than by changing the default.
3. **Read-back is "is it installed?", not "is our record happy?"** — the tile reflects the
   machine, not a note we wrote, and it is asked of `winget list --id`, not of a name match.
4. **No uninstall button on this page.**
5. **In the Phase1 build the Install button is inert** (it reports the command it would have
   run). Phase1's promise is that it is safe to launch anywhere, and a real `winget install`
   would break that promise. No existing Action is gated this way, so this would be new.
6. **Chips are the category list, plus a search box** over name and publisher.
7. **Success includes the "restart to finish" codes.** 3010 / 1641, and "already installed", end
   as installed rather than as failures; the runtimes in this catalogue hit them routinely.
8. **No new logging surface.** The status line, the info bar and the existing file log, as the
   neighbouring Actions already do.

## Left to the builder

- Verify every winget id with `winget show --id <id> --exact --source winget` and fix or drop
  any that do not resolve. Confirm the ids that are per-architecture or per-year
  (`Microsoft.VCRedist.2015+.x64`, `Microsoft.DotNet.DesktopRuntime.8`) point at what we mean.
- Names for the record, the interface and the service, consistent with the existing Apps code.
- Whether the via-winget install should reuse the existing process-running helper, if one exists
  by implementation time, rather than a second copy of the same `ProcessStartInfo`.
- Exact wording of the confirm dialog and the per-tile progress text.
- Whether `winget` missing (no App Installer) is surfaced once at the top of the page or per
  tile. The existing Store path phrases it as an install-app-installer instruction.
