<p align="center">
  <img src="logo/akari-dash.png" alt="Akari Dash logo" width="160" />
</p>

<h1 align="center">Akari Dash</h1>

<p align="center">
  Tune Windows for gaming, see exactly what each Tweak changes, and undo it whenever you like.
</p>

<p align="center">
  <a href="https://github.com/isleap9/Akari-Dash/releases/latest"><b>Download the latest release</b></a>
</p>

---

## What is Akari Dash?

Akari Dash is a gaming dashboard and Windows optimizer, part of the Akari family alongside AkariOS.
It doesn't need AkariOS: it runs on any 64-bit Windows 10 or 11 PC. It puts the PC's gaming,
privacy and Windows settings in one place, and also handles the work around them: removing the apps
Windows ships with, and in time installing software and graphics drivers.

## The goal

Most Windows tweak tools run a script and hope. You can't see what changed, you can't tell what state
the PC is in now, and you can't take it back. Akari Dash is built the other way round:

- **Every change is a Tweak you can read and reverse.** A Tweak is declared as data, never run as a
  script. Akari Dash reads the PC's live state to show which Option each Tweak is in, or Custom when
  it's in none of them. Undo restores the exact values that were there before.
- **Nothing is hidden.** Before you apply anything, you see every registry value, service and
  scheduled task it will touch, and when it takes effect: right away, after sign-out, or after restart.
- **Curated, not copied.** A Tweak ships because it's a decision worth offering on a gaming PC, not
  because another tool happens to have the value.
- **Honest about what can't be undone.** Removing an app or installing a driver is an Action, not a
  Tweak. Akari Dash says so plainly before it starts, and shows progress and a log while it runs.

## What it does

- **Gaming**: latency, GPU scheduling, and the visual effects games pay for
- **Services**: background services and scheduled tasks
- **Privacy**: telemetry, tracking, suggestions and Windows AI
- **Security**: Defender, virtualization-based security and mitigations
- **Windows Update**: when updates arrive, what comes with them, and how loudly
- **Notifications, Windows and Customize**: toasts and tips, theme and system behaviour, and the Start menu, taskbar and File Explorer
- **UWP Apps**: remove Windows apps for good, and bring them back
- **Home**: your PC at a glance, and one button to apply every recommended Tweak

Drivers, Network, Downloads and GPUs are still work in progress.

## Safe to try

- **Preview first.** Before anything is written, Akari Dash lists every value it will change.
- **Undo anything.** The values your PC had before each Tweak (its Original Values) are saved in
  `C:\ProgramData\Akari-Dash`, so Undo puts them back exactly. They survive an uninstall and reinstall.
- **Restore point.** Akari Dash asks Windows for a restore point before the first change of each session.

## Install

1. Download `AkariDash-Setup-<version>.exe` from the [latest release](https://github.com/isleap9/Akari-Dash/releases/latest).
2. Run it and accept the administrator prompt. Akari Dash changes system settings, so it always runs as administrator.

Requires 64-bit Windows 10 (version 2004 or later) or Windows 11.

## Build from source

Needs the .NET 10 SDK, and [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the installer.

```powershell
dotnet build AkariDash.slnx -c Phase1
dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj

# The installer: publishes the Release build, then writes artifacts\installer\AkariDash-Setup-<version>.exe
powershell -ExecutionPolicy Bypass -File installer\Build-Installer.ps1
```

| Configuration | Writes to the system |
|---------------|----------------------|
| `Phase1`      | Never: every apply is a Dry Run, and a banner says so. Safe to run anywhere. |
| `Debug` / `Release` | Yes. Test these in a virtual machine. |

```
AkariDash.slnx
├── src/
│   ├── AkariDash.App         The WinUI 3 app: shell, pages, view models
│   ├── AkariDash.Core        Tweaks, the engine that applies and undoes them, and machine access
│   ├── AkariDash.Framework   Reusable MVVM framework (navigation, settings, dialogs, converters)
│   └── AkariDash.Tests       xUnit tests
├── installer/                Inno Setup script and build script
└── logo/                     The Akari Dash logo
```

Built on .NET 10, the Windows App SDK and WinUI 3, with CommunityToolkit.Mvvm and Microsoft.Extensions.
Vocabulary: [`CONTEXT.md`](CONTEXT.md). Decisions: [`docs/adr/`](docs/adr/).

## Credits

- **[FR33THY](https://github.com/FR33THYFR33THY)**: AkariOS and AkariOS-Ultimate are built around his
  Windows tweaks, and so are many of Akari Dash's. More of them are on the way in future releases.
  Thank you.
- **Winhance**, up to its last MIT-licensed commit: the values behind several Tweaks.

No code is copied from either; every Tweak is rewritten as Akari Dash's own. See
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## License

[MIT](LICENSE)
