# Akari-Dash

A Windows desktop dashboard that optimizes a PC for gaming by applying, reading back, and undoing
system changes. Vocabulary: [`CONTEXT.md`](CONTEXT.md). Decisions: [`docs/adr/`](docs/adr/).

Built from the WinUI-3-MVVM-Framework template: .NET 10, Windows App SDK, WinUI 3,
CommunityToolkit.Mvvm, Microsoft.Extensions DI/Hosting/Logging, xUnit/Moq.

## Solution layout

```
AkariDash.slnx
├── src/
│   ├── AkariDash.Framework   Reusable MVVM framework (navigation, settings, dialogs, converters)
│   ├── AkariDash.App         App shell (Home, Gaming, Settings), wiring via DI
│   └── AkariDash.Tests       xUnit tests
├── Directory.Packages.props  Central package management
├── Directory.Build.props     Shared build settings and configurations
└── global.json               .NET SDK pin
```

The app is unpackaged, self-contained, x64, and its manifest requires administrator.

## Build configurations

| Configuration | Writes to the system | Where it may run |
|---------------|----------------------|------------------|
| `Phase1`      | Never: every apply is a Dry Run; shows a persistent "Dry Run only" banner | Host or VM |
| `Debug` / `Release` | Yes | VM only |

## Build, test, run

```powershell
dotnet build AkariDash.slnx -c Phase1
dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj

# The app requires administrator: launch the built exe (accept the UAC prompt),
# or run `dotnet run` from an elevated terminal.
.\src\AkariDash.App\bin\Phase1\net10.0-windows10.0.26100.0\win-x64\AkariDash.App.exe
```
