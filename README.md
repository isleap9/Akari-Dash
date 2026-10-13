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

## What it does

Akari Dash is a Windows desktop app that optimizes a PC for gaming. Every Tweak shows the exact
registry values, services or scheduled tasks it will change before you apply it, and every change
can be undone.

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

Tweak values come from Akari-OS and AkariOS-Ultimate and from Winhance
up to its last MIT-licensed commit. No code is copied; see [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## License

[MIT](LICENSE)
