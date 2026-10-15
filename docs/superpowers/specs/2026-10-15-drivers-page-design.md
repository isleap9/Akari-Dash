# Drivers page

**Date:** 2026-10-15
**Status:** design; agreed in conversation, not yet built

## What this is

The Drivers page is the **Performance** section's "get a graphics card ready to play" surface. Today it
renders `WorkInProgressView` and nothing else.

This design turns it into the driver pipeline that AkariOS-Ultimate's **Graphics** category performs,
rebuilt as Akari Dash **Actions** (ADR-0004) and written entirely in C#:

- **Clean** the current graphics driver with DDU, through a safe-mode restart.
- **Install** the latest driver for the card that is actually in the PC (NVIDIA, AMD or Intel).
- **Install and configure** — the driver plus the optimised settings profile that goes with it.
- **Debloat** — strip a downloaded vendor installer of its extras, then install it.

Every one of these is an Action: done once, not undoable, warning before it starts, progress while it
runs, a log, and a read-back of what is on the machine. None of it is a Tweak, and none of it is
PowerShell.

## Why now

ADR-0004 put "installing drivers, and resetting them with DDU" in scope as Actions. Issue #33 then did
the *Tweak-shaped* half of AkariOS-Ultimate's Graphics work — the registry — and explicitly deferred
this half: *"the driver pipeline, which is a separate, later, riskier ticket (DDU, safe-mode reboots,
driver downloads)."* This is that ticket.

The Downloads page proved the Action surface (a confirm dialog, a queue, `IStatusBar`/`IInfoBar`
reporting, a Phase 1 that does nothing). Drivers is the second Action page, and the one the page's own
blurb already promises.

## What was decided (by the user, in conversation)

1. **Scope is Drivers only.** GPUs — the registry Tweaks, issue #33 — is a separate story and is not
   touched here, beyond not stepping on it.
2. **Shape is the hybrid** (mockup option C): one guided **"Reset and reinstall the driver"** card for
   the common case, sitting on top of the *same* separate Actions. The card is not a second
   mechanism; it presses the steps below it in order.
3. **Built in two steps: the rows first, the guided wrapper second.** The separate Actions are the
   honest, VM-verifiable primitive; the wrapper that survives two restarts is layered on once they are
   proven.
4. **Everything native C# — no PowerShell.** Downloads, registry, process control, service and task
   control, the BCD edit, the reboot and the appx install all go through .NET/Win32 APIs. The only
   external binaries are the ones that *are* the work (DDU, Nvidia Profile Inspector, the vendor
   installers themselves), launched from C#.
5. **Never tested on the user's machine.** The build lands in `vm-build\AkariDash-Debug`; the steps to
   run inside a VM go in `vm-build\VM-CHECK-DRIVERS.md`.
6. **Vendor-aware.** The page reads the card(s) through the existing `IMachine.GpuVendors()`
   (`WindowsMachine` already maps `VEN_10DE` / `VEN_1002` / `VEN_8086`), and shows only the detected
   vendor's rows.

## Goals

- A Drivers page that detects the graphics card and offers the pipeline for it, and nothing for a
  vendor that is not present.
- **Install latest driver**: find the current driver for the card, download it, run the vendor
  installer silently, and read back the installed version afterwards.
- **Install and configure**: the above, then apply the optimised profile the driver ships with.
- **Debloat**: take a vendor installer the user already downloaded, strip the components AkariOS
  strips, install it silently, and configure it.
- **Clean (DDU)**: remove the current driver through DDU in safe mode, then return the machine able to
  install a fresh one.
- **Guided "Reset and reinstall"**: clean → restart → install → configure behind one button, resuming
  where it left off across the restarts.
- Every step says plainly, before it starts, that it cannot be undone, and shows progress and a
  readable result.
- The Phase 1 build installs and resets nothing; it reports the command lines a real run would have
  used.

## Non-goals

- The registry Tweaks of Graphics — HDCP, P0 state, MSI mode, the raw NVIDIA/AMD/Intel registry
  values as *toggles*. Those are the GPUs page (issue #33).
- Removing or replacing drivers by hand outside these Actions.
- Installing ordinary applications — Downloads owns that.
- Version pinning, downgrading, or "update my driver when Windows Update has one".
- Live monitoring, stress tests, benchmarks (ADR-0004 keeps these out).
- A download *page*: the downloads here are the driver and its tools, into a temp folder, not a
  catalogue.

## Anti-goals

- Presenting a driver install or a DDU reset as a **Tweak**. It has no Original Values and no Undo;
  showing it as reversible would be a lie the rest of the app is built to avoid.
- Starting anything irreversible without saying so first, in plain words, naming the card.
- Shipping a `.ps1`, or requiring PowerShell or a PowerShell execution-policy change.
- Leaving a machine with no graphics driver and no way forward — every path must end able to reach a
  driver, or say clearly what to do if it cannot.
- Silently installing a *third-party tool* (a driver de-bloater, a profile editor) the user did not
  agree to.

## Constraints from the repository

- **Action semantics** (ADR-0004, `CONTEXT.md`): no Undo, a plain warning before it starts, progress
  while it runs, a log, and read-back of whether it happened. A change that can be undone is never an
  Action; an Action never pretends to be a Tweak.
- **Content is data, not scripts** (ADR-0001). Amended 2026-10-13: an Action *may* start a process — a
  package manager, an installer, DDU — "because that is what installing software is". It also says the
  step that has to run in safe mode, where a WinUI app cannot, is **"a small console exe beside the
  app rather than a `.ps1` written to disk and launched through RunOnce with the execution policy
  bypassed."** That is exactly the shape of the clean step below.
- **Never test on real hardware.** Everything lands in `vm-build\AkariDash-Debug` and is tested in a
  VM, with a `VM-CHECK-*.md` beside it (the existing convention).
- **Phase 1 is inert** (`BuildInfo.IsDryRunOnly`). No existing Action is gated this way yet — the
  Downloads page introduced the pattern; Drivers follows it.
- **Match the neighbours.** `DownloadsViewModel` / `DownloadsPage.xaml` are the closest model: a
  view model taking `IDialogService`, `IInfoBarService`, `IStatusService`, `ILogger`, implementing
  `INavigationAware`, with a confirm before an Action, a status line + `ProgressRing`, per-item state
  and a final summary.
- **Naming collision to avoid.** `DriversViewModel` **already exists** — it is the *UWP Apps page's*
  Drivers tab (driver-store cleanup). The new page's view model needs a different name (e.g.
  `DriverPipelineViewModel`). `DriversPage.xaml` is the placeholder to replace.
- **Vendor-awareness already exists.** `GpuVendor`, `IMachine.GpuVendors()`, and
  `DeclaredTweak.RequiresGpu` (which drives **Unavailable** with a reason) are in place. The pipeline
  should reuse the same detection rather than a second copy.
- **Tests** in `src/AkariDash.Tests` (xUnit). `AkariDash.Tests` references `AkariDash.Core` and
  `AkariDash.Framework` only — never `AkariDash.App`. Everything testable therefore lives in Core as
  data and pure functions (the driver-source URLs, the debloat strip lists, the command lines, the
  parse of each vendor's "latest" answer); the service, the page and its view model are verified by
  building and by the VM check.
- **Commits:** one per task, message style as the repo uses it (`Area: what changed`).

## How it works

### The pipeline as data (Core)

A declared description of each step, in the spirit of the Tweak catalogue's data files:

- `DriverStep` (enum): `Clean`, `Install`, `InstallAndConfigure`, `Debloat`.
- One record per step: its id, title, one-line description, which **vendor(s)** it applies to, whether
  it needs a **restart**, and its **risk** (Safe / Caution / Advanced, as AkariOS labels them).
- The **vendor sources** as data: the NVIDIA GeForce lookup endpoint, the AMD download-page pattern,
  the Intel path; the silent-install argument sets; and the **debloat strip lists** (the NVIDIA module
  folders, the AMD `Config` XML/JSON keys, the Intel extras).
- The **command lines**, built in Core so the Phase 1 build can report exactly what a real run would
  have run — the same trick `WingetCommand` uses for Downloads.

The strip lists, the URLs and the argument sets are all directly unit-testable.

### The service (Core interface, App implementation)

Following `IAppInstallService` / `WindowsWingetAppService`, an interface in `AkariDash.Core` and a
Windows implementation in `src/AkariDash.App/Services`:

- `bool IsDryRun { get; }` — Phase 1.
- `Task<InstalledDriver?> CurrentAsync(GpuVendor vendor, ct)` — the driver version on the machine now,
  for the read-back. `null` means "could not ask", never "none".
- `Task<AvailableDriver?> LatestAsync(GpuVendor vendor, ct)` — the vendor's current driver (version +
  download URL), or `null` when the vendor's endpoint cannot be read.
- `Task<DriverStepResult> InstallAsync(...)`, `InstallAndConfigureAsync(...)`, `DebloatAsync(file, ...)`,
  `CleanAsync(mode, ...)`.

Installs run one at a time through a queue, as Downloads does, because two vendor installers at once is
never right.

### Reading each vendor's latest driver

- **NVIDIA**: the GeForce *AjaxDriverService* JSON endpoint AkariOS uses, parsed with
  `System.Text.Json`; the download URL is assembled from the returned version. (AkariOS's product/page
  ids are NVIDIA-specific and must be re-verified for the cards we mean.)
- **AMD**: the drivers page's Adrenalin link, found by the same URL pattern AkariOS matches, fetched
  with an ordinary `HttpClient`. The pattern and the page both change over time — this is a known
  maintenance point, and it must fail to *unknown* rather than to a wrong URL.
- **Intel**: the download page, or a parsed link if one can be found. AkariOS only opens the page; the
  minimum here is not to guess.

### Reading the machine back

`CurrentAsync` reads the installed driver version from the Display class keys the existing
`WindowsMachine` already walks, so the page can say "NVIDIA 566.36 installed — 572.16 available". This
is the read-back an Action owes: it reports the machine, not a note we wrote.

### The safe-mode clean, and the companion exe

A DDU clean cannot run from the WinUI app: it needs safe mode, and the app is not running then. Per
ADR-0001's amendment, this is a **small console exe beside the app**, not a `.ps1`:

- A second project, e.g. `src/AkariDash.DriverReset` (`AkariDash.DriverReset.exe`), shipped next to the
  app by the installer.
- The app **stages everything first**: it downloads DDU *and the replacement driver and its tools* into
  a temp folder, so the machine is only wiped once a way back is already on disk. Then it writes DDU's
  settings file (read-only), registers the companion via `RunOnce`, sets `safeboot minimal` (BCD), and
  reboots.
- The companion, in safe mode: clears the `safeboot` flag, runs DDU with the agreed flags, writes its
  result where the app can read it, and reboots back to normal.
- On the way back, the app reads the companion's result and offers the next step (install).

The BCD edit is `bcdedit.exe` driven from C# (or the BCD WMI provider); the reboot is
`InitiateSystemShutdownEx`; the `RunOnce` write is `Registry`. None of it is PowerShell.

### The guided "Reset and reinstall" (second step, after the rows)

One button that chains clean → restart → install → configure, resuming across the restarts. Because it
spans reboots, it keeps its own small state file under `ProgramData\Akari-Dash` recording which step is
done, so a restart in the middle does not lose the run — and, if the PC is turned off mid-way, the page
can offer to resume or to stop. This is deliberately **built last**, once each step is proven on its
own.

### Page and view model

`DriversPage.xaml` replaces the placeholder with: a header naming the detected card, the guided card
(second step), the rows — each a title, a one-line description, a risk label and its button(s) — the
status line with a `ProgressRing`, and the info bar for results. A new view model
(`DriverPipelineViewModel`, name TBD) supplies them, confirms before an irreversible step, runs the
queue, and reports. Register it `AddTransient` in `App.xaml.cs`; register the service `AddSingleton`,
beside the other Action backends. Give the nav item a real subtitle (it currently reads "Work in
progress").

### VM check

`vm-build/VM-CHECK-DRIVERS.md`, in the style of `VM-CHECK-UWP-APPS.md` / `VM-CHECK-DOWNLOADS.md`:
numbered steps, each saying what to do and what to expect, with an independent way to confirm each
result inside the VM (Add/Remove Programs, `pnputil /enum-drivers`, `nvidia-smi`, or the vendor tool).
The DDU clean gets its own careful sequence, including how to recover if a step fails.

## Calls I made (please check these)

1. **The configure step lives on Drivers, as part of an install** — "Install and configure" is one
   Action. The *toggles* for the same settings stay on GPUs (#33). To avoid two sources of truth, the
   settings values should live in one place in Core, shared by both pages, even though they surface
   differently.
2. **DirectX and the C++ runtimes are dropped from Drivers.** Downloads already installs the VC++
   redistributable through winget, and DirectX belongs with it. Duplicating them here would create a
   second, worse installer path. (They can move to Downloads later if wanted.)
3. **The two "open a Windows page" entries (Display settings, Advanced graphics/HAGS) are dropped.**
   They are `ms-settings:` links, not Actions with anything to install or read back, and the app is not
   a launcher for Windows' own pages.
4. **7-Zip is not bundled.** The debloat uses the vendor installer's own extraction switch where it has
   one, and a managed archive library otherwise — no shipped `7z.exe`, and no installing 7-Zip as a
   side effect of a driver task.
5. **DDU and Nvidia Profile Inspector are downloaded at run time from their official sources**, not
   bundled, and the user is told they are being fetched. (Bundling raises licence and update
   questions; AkariOS downloads them too.)
6. **Phase 1 is inert for every Drivers Action**, reporting the command lines it would have run —
   new behaviour for an Action, as Downloads introduced, extended to a reboot-bearing step where the
   simulated run stops at the point a real one would reboot.
7. **Read-back is the installed driver version**, asked of the machine, so "up to date" is a fact and
   not a guess.
8. **The guided wrapper's state is a small file under `ProgramData\Akari-Dash`**, so it survives the
   restarts a clean needs.
9. **A machine with more than one card gets one section per detected vendor.** A laptop with an Intel
   iGPU and an NVIDIA discrete card shows the NVIDIA pipeline *and* the Intel one, each acting on its
   own card. An Action never silently touches a card the user did not name — in particular the clean
   targets **only the selected vendor's** driver, never `CleanAllGpus`. (AkariOS wipes every GPU; this
   does not, because wiping a driver nobody asked about is exactly the anti-goal.) The guided card
   takes the discrete card as its subject and says which one.
10. **The replacement driver is fetched and staged *before* the clean and the reboot.** DDU, the
   vendor installer and any profile tool land in a temp folder first; only then does the machine get
   wiped. That is what keeps the anti-goal honest — if the vendor endpoint is down, the run stops
   *before* the driver is removed, not after.
11. **The configure step applies a settings *bundle* as part of an install.** It is not a set of
   toggles: it is \"install the driver and put the profile on\". The individual toggles over the same
   values remain the GPUs page's business (#33). The values themselves are defined once, in Core, and
   consumed by both.
12. **The clean does not make a lasting change to Windows Update.** Release the driver-search order or
   the `SearchOrderConfig` value permanently and the change is undoable state, which per the Tweak
   model belongs on the GPUs/Tweaks side, not inside an Action. DDU's own run-scoped
   `PreventWinUpdate` is used; nothing is written that outlives the run.
13. **Clean has two modes.** `Auto` runs DDU unattended; `Manual` opens DDU for the user to drive.
   Both are rows (hence a `mode` on the service call), matching how AkariOS ships them, and the manual
   one carries its own \"you are on your own from here\" wording.
14. **Debloat takes its file from a fixed folder, not a picker.** The app runs elevated and the repo
   already documents that file pickers do not open there (`SystemCleanupViewModels.cs:242`); the
   existing Drivers tab works around it by writing to a fixed `Documents\Akari-Dash\...` folder and
   opening Explorer. Debloat does the same: the user drops the installer in
   `Documents\Akari-Dash\Downloaded drivers`, the row lists the `.exe` files found there and the
   user picks one, and an empty folder says where to put it and offers to open it. No picker of
   either kind.

## Left to the builder

- **Verify every vendor endpoint** (NVIDIA lookup, AMD pattern, Intel path) as they are now, not as
  AkariOS wrote them in 2024 — AMD and Intel pages change; a broken source must read as *unknown*.
- **Find the canonical, current download URLs** for DDU and Nvidia Profile Inspector, and for the
  NVIDIA Control Panel `.appx`; confirm each against its own source before shipping.
- **Settle the DDU flag set and the safe-mode entry/exit sequence**, and write the recovery path; this
  is the riskiest thing in the design and gets its own VM pass.
- **Confirm whether the AMD and Intel installers can genuinely run unattended.** AkariOS launches
  AMD's web installer with no switch and Intel's with `-s`; if there is no reliable silent path, that
  row must say so plainly and hand off to the vendor's own wizard — which breaks the \"progress and
  read-back\" promise for that vendor and must be reflected in the row's wording, not hidden.
- **Decide the exact debloat strip lists** from the AkariOS lists, dropping anything that is not safe
  to remove on current drivers.
- **Name** the new page view model (the existing `DriversViewModel` is taken) and the service, in the
  house style.
- **The installer** (`installer/Build-Installer.ps1`): shipping `AkariDash.DriverReset.exe` beside the
  app, and whether the companion is a separate binary or a second entry point.
- **Exact wording** of the confirms and the progress text, and how the detected card is phrased when
  the PC has more than one GPU (an iGPU and a discrete card).
- **Whether the same process-running helper** the other Action services use should be shared, rather
  than a second copy of `ProcessStartInfo`.
