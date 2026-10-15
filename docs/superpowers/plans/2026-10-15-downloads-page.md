# Downloads Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Downloads placeholder into a curated grid of installable apps, where clicking a tile installs that app through `winget`.

**Architecture:** The catalogue and everything that decides a winget answer live in `AkariDash.Core` as data and pure functions and are unit-tested there. A thin Windows service in `AkariDash.App` runs the two winget commands. The page and its view model follow the UWP Apps page's house pattern: chips and search over a grid, `IDialogService` to confirm, `IStatusService` for the status line, `IInfoBarService` for results, and a queue so several tiles can be queued at once.

**Tech Stack:** .NET 10, WinUI 3 (Windows App SDK), CommunityToolkit.Mvvm, Microsoft.Extensions DI/logging, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-15-downloads-page-design.md`

## Global Constraints

- **`AkariDash.Tests` references `AkariDash.Core` and `AkariDash.Framework` only — never `AkariDash.App`.** All unit-testable logic therefore belongs in Core. The view model and the service are verified by building and by the VM check, not by xUnit.
- **Target framework is `net10.0-windows10.0.26100.0`, `Nullable` enabled, `ImplicitUsings` enabled.**
- **Configurations are `Debug;Release;Phase1`.** `Phase1` sets `BuildInfo.IsDryRunOnly` (see `src/AkariDash.App/BuildInfo.cs`) and must never install anything.
- **An install is an Action (ADR-0004):** not undoable, warns first, reports progress, logs, and reads back whether it happened. It is never shown as a Tweak.
- **Silent installs only**, with the exact flag set already used in `src/AkariDash.App/Services/WindowsStoreAppService.cs:206-219`: `install --id <id> --source winget --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity`.
- **Never test on real hardware.** Everything lands in `vm-build\AkariDash-Debug` and is tested in a VM.
- **Follow the neighbours' patterns.** `UninstallerViewModel` for dialogs/status/info bar, `StoreAppsViewModel` for list + filter, `TunerChipViewModel` for chips, `HomePage.xaml` for `ItemsRepeater` grids, `CustomizePage` for a page taking its view model by constructor injection.
- **Commits:** one per task, message style as the repo uses it (`Area: what changed`).

## Review Focus

Carried forward from the spec; each line is pinned by a test in the task that owns the code.

1. **`Valve.Steam` reported installed because `Valve.SteamLink` is.** Matching a package id as a substring of winget's output is wrong; it must match a whole token. (Task 3)
2. **A runtime that needs a restart shown as a failure.** `winget` returns 3010 / 1641 for "installed, reboot required", and the runtimes in this catalogue hit it every time. (Task 3)
3. **winget absent, so every tile claims "not installed".** The read-back must distinguish "asked and not there" from "could not ask". (Task 3)
4. **An id with a `+` in it going through a regex or an escaped comparison.** `Microsoft.VCRedist.2015+.x64` is in the catalogue. (Task 3)
5. **An already-installed package reported as a failure** rather than as "already installed". (Task 3)

---

### Task 1: The catalogue

**Files:**
- Create: `src/AkariDash.Core/Apps/InstallableApp.cs`
- Create: `src/AkariDash.Core/Apps/InstallableApps.cs`
- Test: `src/AkariDash.Tests/InstallableAppsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `InstallableAppCategory` (enum: `Launchers`, `Communication`, `Tools`, `Monitoring`, `Runtimes`); `InstallableApp(string WingetId, string Name, string Publisher, string Description, InstallableAppCategory Category, string AccentColor)`; `InstallableApps.All` (`IReadOnlyList<InstallableApp>`).

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class InstallableAppsTests
{
    [Fact]
    public void Every_app_is_listed_once()
    {
        Assert.Equal(
            InstallableApps.All.Count,
            InstallableApps.All.Select(app => app.WingetId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_app_is_complete()
    {
        Assert.All(InstallableApps.All, app =>
        {
            Assert.False(string.IsNullOrWhiteSpace(app.WingetId));
            Assert.False(string.IsNullOrWhiteSpace(app.Name));
            Assert.False(string.IsNullOrWhiteSpace(app.Publisher));
            Assert.False(string.IsNullOrWhiteSpace(app.Description));
            // A winget id is Publisher.Package: at least one dot, no spaces, no leading or trailing dot.
            Assert.Matches(@"^[^\s.]+\.[^\s.]+(\.[^\s.]+)*$", app.WingetId);
            Assert.Matches(@"^#[0-9A-Fa-f]{6}$", app.AccentColor);
        });
    }

    [Fact]
    public void Every_category_has_something_in_it()
    {
        Assert.All(Enum.GetValues<InstallableAppCategory>(), category =>
            Assert.Contains(InstallableApps.All, app => app.Category == category));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~InstallableAppsTests"`
Expected: FAIL to compile — `InstallableApp` does not exist.

- [ ] **Step 3: Write `InstallableApp.cs`**

```csharp
namespace AkariDash.Core.Apps;

/// <summary>Which filter chip an installable app falls under.</summary>
public enum InstallableAppCategory
{
    Launchers,
    Communication,
    Tools,
    Monitoring,
    Runtimes,
}

/// <summary>
/// One app the Downloads page can install: its winget package id, what to call it, who publishes it,
/// one line on what it is, its filter chip, and the colour of its tile's avatar square.
/// </summary>
public sealed record InstallableApp(
    string WingetId,
    string Name,
    string Publisher,
    string Description,
    InstallableAppCategory Category,
    string AccentColor);
```

- [ ] **Step 4: Write `InstallableApps.cs`**

A static class with `public static IReadOnlyList<InstallableApp> All { get; } = [ ... ];` declared in
the style of `src/AkariDash.Core/Apps/RestorableApps.cs`, using exactly these rows in this order:

| Category | Name | Publisher | WingetId | AccentColor |
|---|---|---|---|---|
| Launchers | Steam | Valve | `Valve.Steam` | `#1B2838` |
| Launchers | Epic Games Launcher | Epic Games | `EpicGames.EpicGamesLauncher` | `#2A2A2A` |
| Launchers | Battle.net | Blizzard | `Blizzard.BattleNet` | `#00AEFF` |
| Launchers | GOG Galaxy | GOG | `GOG.Galaxy` | `#6C2BD9` |
| Launchers | EA App | Electronic Arts | `ElectronicArts.EADesktop` | `#E4002B` |
| Launchers | Ubisoft Connect | Ubisoft | `Ubisoft.Connect` | `#0070FF` |
| Launchers | Rockstar Games Launcher | Rockstar Games | `RockstarGames.Launcher` | `#FCAF17` |
| Communication | Discord | Discord Inc. | `Discord.Discord` | `#5865F2` |
| Tools | OBS Studio | OBS Project | `OBSProject.OBSStudio` | `#302E31` |
| Tools | 7-Zip | Igor Pavlov | `7zip.7zip` | `#1F5FA9` |
| Tools | ShareX | ShareX Team | `ShareX.ShareX` | `#2E8B57` |
| Tools | Notepad++ | Notepad++ Team | `Notepad++.Notepad++` | `#90C31B` |
| Tools | PowerToys | Microsoft | `Microsoft.PowerToys` | `#0078D4` |
| Tools | Everything | voidtools | `voidtools.Everything` | `#E8762C` |
| Tools | VLC | VideoLAN | `VideoLAN.VLC` | `#FF8800` |
| Monitoring | MSI Afterburner | MSI | `Guru3D.Afterburner` | `#B01C1C` |
| Monitoring | HWiNFO | Martin Malík | `REALiX.HWiNFO` | `#16794A` |
| Monitoring | CPU-Z | CPUID | `CPUID.CPU-Z` | `#0F5FA6` |
| Monitoring | GPU-Z | TechPowerUp | `TechPowerUp.GPU-Z` | `#2F6F9F` |
| Runtimes | Visual C++ 2015–2022 (x64) | Microsoft | `Microsoft.VCRedist.2015+.x64` | `#0078D4` |
| Runtimes | .NET Desktop Runtime 8 | Microsoft | `Microsoft.DotNet.DesktopRuntime.8` | `#512BD4` |

`Description` is one short sentence in `RestorableApps`' voice, ending with a period — for example
*"Play, install and update PC games."* for Steam and *"Files, photos and other programs a game
needs before it will start."* for the Visual C++ redistributable.

- [ ] **Step 5: Verify every id resolves**

For each row, run:

```powershell
winget show --id <WingetId> --exact --source winget --accept-source-agreements
```

Expected: every id is found. Fix or drop any that is not, and if an id changes, add its new name to
the table in the spec so the two stay together. Two rows need care: `Microsoft.VCRedist.2015+.x64`
and `Microsoft.DotNet.DesktopRuntime.8` — confirm the redistributable resolves to the 2015-2022
x64 package and not a per-year one, and that the runtime id is the one this project already
targets.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~InstallableAppsTests"`
Expected: PASS, 3 tests.

- [ ] **Step 7: Commit**

```bash
git add src/AkariDash.Core/Apps/InstallableApp.cs src/AkariDash.Core/Apps/InstallableApps.cs src/AkariDash.Tests/InstallableAppsTests.cs
git commit -m "Downloads: the catalogue of apps to install"
```

---

### Task 2: The winget command lines

**Files:**
- Create: `src/AkariDash.Core/Apps/InstallOutcome.cs`
- Create: `src/AkariDash.Core/Apps/WingetCommand.cs`
- Test: `src/AkariDash.Tests/WingetCommandTests.cs`

**Interfaces:**
- Consumes: `InstallableApp` (Task 1).
- Produces: `InstallOutcome` (enum: `Installed`, `InstalledNeedsRestart`, `AlreadyInstalled`); `WingetCommand.List()` → `IReadOnlyList<string>`; `WingetCommand.Install(InstallableApp app)` → `IReadOnlyList<string>`; `WingetCommand.Describe(IReadOnlyList<string> arguments)` → `string`.

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class WingetCommandTests
{
    private static InstallableApp App(string id) =>
        new(id, "Name", "Publisher", "Description.", InstallableAppCategory.Tools, "#0078D4");

    [Fact]
    public void Install_is_exact_silent_and_agrees_to_everything()
    {
        Assert.Equal(
            new[]
            {
                "install", "--id", "Valve.Steam", "--source", "winget", "--exact", "--silent",
                "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
            },
            WingetCommand.Install(App("Valve.Steam")));
    }

    [Fact]
    public void Install_does_not_mangle_an_id_with_a_plus_in_it()
    {
        Assert.Contains(
            "Microsoft.VCRedist.2015+.x64",
            WingetCommand.Install(App("Microsoft.VCRedist.2015+.x64")));
    }

    [Fact]
    public void List_asks_for_no_interaction()
    {
        var arguments = WingetCommand.List();
        Assert.Equal("list", arguments[0]);
        Assert.Contains("--disable-interactivity", arguments);
    }

    [Fact]
    public void Describe_is_the_line_a_person_would_type()
    {
        Assert.Equal(
            "winget install --id Valve.Steam --source winget",
            WingetCommand.Describe(["install", "--id", "Valve.Steam", "--source", "winget"]));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~WingetCommandTests"`
Expected: FAIL to compile — `WingetCommand` does not exist.

- [ ] **Step 3: Write `InstallOutcome.cs`**

```csharp
namespace AkariDash.Core.Apps;

/// <summary>How an install ended. A failure is not one of these; <see cref="IAppInstallService.InstallAsync"/> throws instead.</summary>
public enum InstallOutcome
{
    /// <summary>Installed and ready.</summary>
    Installed,

    /// <summary>Installed, but Windows wants a restart before it is usable.</summary>
    InstalledNeedsRestart,

    /// <summary>It was already on the machine; nothing was downloaded.</summary>
    AlreadyInstalled,
}
```

- [ ] **Step 4: Write `WingetCommand.cs`**

```csharp
namespace AkariDash.Core.Apps;

/// <summary>
/// The winget command lines the Downloads page runs. Kept here beside the catalogue so the Phase 1
/// build can show exactly what a real install would have run without running it.
/// </summary>
public static class WingetCommand
{
    public static IReadOnlyList<string> Install(InstallableApp app) =>
    [
        "install", "--id", app.WingetId, "--source", "winget", "--exact", "--silent",
        "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity",
    ];

    public static IReadOnlyList<string> List() =>
    [
        "list", "--accept-source-agreements", "--disable-interactivity",
    ];

    public static string Describe(IReadOnlyList<string> arguments) => "winget " + string.Join(' ', arguments);

    public static string DescribeInstall(InstallableApp app) => Describe(Install(app));
}
```

Do not quote or escape `app.WingetId`: it goes into an argument list, not a shell command line.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~WingetCommandTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Commit**

```bash
git add src/AkariDash.Core/Apps/InstallOutcome.cs src/AkariDash.Core/Apps/WingetCommand.cs src/AkariDash.Tests/WingetCommandTests.cs
git commit -m "Downloads: the winget command lines, as data"
```

---

### Task 3: Reading winget's answers

**Files:**
- Create: `src/AkariDash.Core/Apps/WingetInstallResult.cs`
- Create: `src/AkariDash.Core/Apps/WingetListOutput.cs`
- Test: `src/AkariDash.Tests/WingetInstallResultTests.cs`
- Test: `src/AkariDash.Tests/WingetListOutputTests.cs`

**Interfaces:**
- Consumes: `InstallableApp` (Task 1), `InstallOutcome` (Task 2).
- Produces: `WingetInstallResult.Interpret(int exitCode)` → `InstallOutcome?`; `WingetListOutput.InstalledIds(string? wingetListOutput, IReadOnlyList<InstallableApp> apps)` → `IReadOnlySet<string>?`.

- [ ] **Step 1: Write the failing test for the exit codes**

```csharp
using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class WingetInstallResultTests
{
    [Fact]
    public void Zero_is_installed() => Assert.Equal(InstallOutcome.Installed, WingetInstallResult.Interpret(0));

    // ERROR_SUCCESS_REBOOT_REQUIRED and ERROR_SUCCESS_REBOOT_INITIATED: the runtimes return these.
    [Theory]
    [InlineData(3010)]
    [InlineData(1641)]
    public void A_restart_code_is_still_installed(int exitCode) =>
        Assert.Equal(InstallOutcome.InstalledNeedsRestart, WingetInstallResult.Interpret(exitCode));

    // APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED, which arrives as a negative int.
    [Fact]
    public void Already_installed_is_not_a_failure() =>
        Assert.Equal(InstallOutcome.AlreadyInstalled, WingetInstallResult.Interpret(unchecked((int)0x8A150061)));

    [Theory]
    [InlineData(1)]
    [InlineData(unchecked((int)0x8A15002B))] // no applicable installer found
    [InlineData(-1)]
    public void Anything_else_is_a_failure(int exitCode) => Assert.Null(WingetInstallResult.Interpret(exitCode));
}
```

- [ ] **Step 2: Write the failing test for the installed list**

```csharp
using AkariDash.Core.Apps;
using Xunit;

namespace AkariDash.Tests;

public class WingetListOutputTests
{
    private static InstallableApp App(string id) =>
        new(id, id, "Publisher", "Description.", InstallableAppCategory.Tools, "#0078D4");

    private static readonly InstallableApp[] Catalogue =
        [App("Valve.Steam"), App("GOG.Galaxy")];

    [Fact]
    public void A_null_output_means_we_could_not_ask() =>
        Assert.Null(WingetListOutput.InstalledIds(null, Catalogue));

    [Fact]
    public void An_id_in_the_output_is_an_installed_app()
    {
        const string output = """
            Name                 Id            Version
            ------------------------------------------
            Steam                Valve.Steam   1.0.0
            """;

        var installed = WingetListOutput.InstalledIds(output, Catalogue);

        Assert.NotNull(installed);
        Assert.Equal(["Valve.Steam"], installed);
    }

    [Fact]
    public void A_longer_id_that_merely_starts_the_same_is_not_the_same_app()
    {
        const string output = """
            Name                 Id                 Version
            -------------------------------------------------
            Steam Link           Valve.SteamLink    1.0.0
            """;

        var installed = WingetListOutput.InstalledIds(output, Catalogue);

        Assert.NotNull(installed);
        Assert.Empty(installed);
    }

    [Fact]
    public void An_id_with_a_plus_is_matched_literally()
    {
        var catalogue = new[] { App("Microsoft.VCRedist.2015+.x64") };
        const string output = """
            Name                                                  Id                              Version
            --------------------------------------------------------------------------------------------
            Microsoft Visual C++ 2015-2022 Redistributable (x64)  Microsoft.VCRedist.2015+.x64    14.40.0
            """;

        var installed = WingetListOutput.InstalledIds(output, catalogue);

        Assert.NotNull(installed);
        Assert.Contains("Microsoft.VCRedist.2015+.x64", installed);
    }

    // A narrow console makes winget truncate the Id column, and a truncated id cannot be told
    // apart from another that shares its prefix - so we do not claim to know what is missing.
    [Fact]
    public void A_truncated_output_says_we_do_not_know() =>
        Assert.Null(WingetListOutput.InstalledIds(
            """
            Name                                                  Id                            Version
            --------------------------------------------------------------------------------------------
            Microsoft Visual C++ 2015-2022 Redistributable (x64)  Microsoft.VCRedist.2015+.x…   14.40.0
            """,
            [App("Microsoft.VCRedist.2015+.x64")]));
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~Winget"`
Expected: FAIL to compile — `WingetInstallResult` and `WingetListOutput` do not exist.

- [ ] **Step 4: Write `WingetInstallResult.cs`**

```csharp
namespace AkariDash.Core.Apps;

/// <summary>What winget's exit code means for an install.</summary>
public static class WingetInstallResult
{
    /// <summary>
    /// How the install ended, or <see langword="null"/> when the exit code is a real failure and the
    /// caller should throw with winget's own last words. Codes from winget's published error list:
    /// 3010 and 1641 are the Windows "succeeded, restart needed" pair, and 0x8A150061 is
    /// APPINSTALLER_CLI_ERROR_PACKAGE_ALREADY_INSTALLED.
    /// </summary>
    public static InstallOutcome? Interpret(int exitCode) => exitCode switch
    {
        0 => InstallOutcome.Installed,
        3010 or 1641 => InstallOutcome.InstalledNeedsRestart,
        unchecked((int)0x8A150061) => InstallOutcome.AlreadyInstalled,
        _ => null,
    };
}
```

- [ ] **Step 5: Write `WingetListOutput.cs`**

```csharp
namespace AkariDash.Core.Apps;

/// <summary>Which of the catalogue's apps winget says are already on this machine.</summary>
public static class WingetListOutput
{
    /// <summary>
    /// The ids winget listed, or <see langword="null"/> when we cannot say - winget was not available,
    /// or its output was truncated so that "not listed" would not mean "not installed".
    /// </summary>
    public static IReadOnlySet<string>? InstalledIds(string? wingetListOutput, IReadOnlyList<InstallableApp> apps)
    {
        if (wingetListOutput is null)
        {
            return null;
        }

        var tokens = wingetListOutput
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        // A truncated id shares its prefix with another package, so a missing one proves nothing.
        if (tokens.Any(token => token.EndsWith('…')))
        {
            return null;
        }

        var listed = new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase);
        return apps.Where(app => listed.Contains(app.WingetId))
            .Select(app => app.WingetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~Winget"`
Expected: PASS, 15 tests.

- [ ] **Step 7: Commit**

```bash
git add src/AkariDash.Core/Apps/WingetInstallResult.cs src/AkariDash.Core/Apps/WingetListOutput.cs src/AkariDash.Tests/WingetInstallResultTests.cs src/AkariDash.Tests/WingetListOutputTests.cs
git commit -m "Downloads: reading winget's exit codes and its installed list"
```

---

### Task 4: The install service

**Files:**
- Create: `src/AkariDash.Core/Apps/IAppInstallService.cs`
- Create: `src/AkariDash.App/Services/WindowsWingetAppService.cs`
- Modify: `src/AkariDash.App/App.xaml.cs:184-189` (service registrations)

**Interfaces:**
- Consumes: `InstallableApp` (Task 1), `InstallOutcome` (Task 2), `WingetCommand` / `WingetInstallResult` / `WingetListOutput` (Tasks 2-3), `BuildInfo.IsDryRunOnly`.
- Produces: `IAppInstallService` with `bool IsDryRun { get; }`, `Task<IReadOnlySet<string>?> FindInstalledIdsAsync(IReadOnlyList<InstallableApp> apps, CancellationToken cancellationToken = default)`, `Task<InstallOutcome> InstallAsync(InstallableApp app, CancellationToken cancellationToken = default)`; `WindowsWingetAppService : IAppInstallService`.

- [ ] **Step 1: Write `IAppInstallService.cs`**

```csharp
namespace AkariDash.Core.Apps;

/// <summary>
/// The Downloads page's backend: which of the catalogue is already installed, and installing one.
/// An Action backend (ADR-0004): an install runs once, cannot be undone, and is never a Tweak.
/// </summary>
public interface IAppInstallService
{
    /// <summary>
    /// True in the Phase 1 build, where nothing is installed and the page only says what it would
    /// have run. Phase 1 promises it is safe to launch anywhere; a real install would break that.
    /// </summary>
    bool IsDryRun { get; }

    /// <summary>
    /// The catalogue's ids that are installed, or <see langword="null"/> when winget could not be
    /// asked - in which case the page says so rather than claiming everything is missing.
    /// </summary>
    Task<IReadOnlySet<string>?> FindInstalledIdsAsync(
        IReadOnlyList<InstallableApp> apps,
        CancellationToken cancellationToken = default);

    /// <summary>Installs one app. Throws with a readable message when it fails.</summary>
    Task<InstallOutcome> InstallAsync(InstallableApp app, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Write `WindowsWingetAppService.cs`**

In `src/AkariDash.App/Services/`, in the style of `WindowsInstalledAppService` (primary
constructor, `sealed class ... : IAppInstallService`):

- `public bool IsDryRun => BuildInfo.IsDryRunOnly;`
- `FindInstalledIdsAsync`: run `WingetCommand.List()` through one private `RunAsync` helper,
  return `WingetListOutput.InstalledIds(output, apps)`, and return `null` when the process cannot
  be started or the command fails to run at all.
- `InstallAsync`: run `WingetCommand.Install(app)`, then
  `WingetInstallResult.Interpret(exitCode)`; when that is `null`, throw
  `InvalidOperationException` naming the app and carrying winget's last non-empty output line, in
  the phrasing style of `WindowsStoreAppService.InstallFromStoreAsync` — including the case where
  the process cannot be started at all ("winget is not available (...)").
- **The private `RunAsync` helper** builds `ProcessStartInfo("winget")` with the argument list,
  `UseShellExecute = false`, `CreateNoWindow = true`, `RedirectStandardOutput = true`,
  `RedirectStandardError = true`, and **reads stdout and stderr concurrently**
  (`await Task.WhenAll(stdout.ReadToEndAsync(ct), stderr.ReadToEndAsync(ct))`) before waiting for
  exit. Do not copy the sequential read from `WindowsStoreAppService`: a chatty installer can fill
  the stderr pipe and deadlock against a stdout read that is waiting for exit.
- Log the command line being run with `ILogger<WindowsWingetAppService>`, and in the dry-run case
  log the same line and install nothing.

- [ ] **Step 3: Register the service**

In `src/AkariDash.App/App.xaml.cs`, beside the other Action backends (around lines 184-189):

```csharp
builder.Services.AddSingleton<IAppInstallService, WindowsWingetAppService>();
```

- [ ] **Step 4: Build and verify it compiles**

Run: `dotnet build AkariDash.slnx -c Phase1`
Expected: build succeeded, 0 errors.

- [ ] **Step 5: Run the whole test suite to verify nothing regressed**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj`
Expected: PASS, no failures.

- [ ] **Step 6: Commit**

```bash
git add src/AkariDash.Core/Apps/IAppInstallService.cs src/AkariDash.App/Services/WindowsWingetAppService.cs src/AkariDash.App/App.xaml.cs
git commit -m "Downloads: run winget for installs and read back what is on the machine"
```

---

### Task 5: The view model

**Files:**
- Create: `src/AkariDash.App/ViewModels/AppTileViewModel.cs`
- Create: `src/AkariDash.App/ViewModels/DownloadsViewModel.cs`
- Modify: `src/AkariDash.App/App.xaml.cs:170-181` (page view model registrations)

**Interfaces:**
- Consumes: `IAppInstallService`, `InstallOutcome`, `InstallableApp`, `InstallableApps`, `WingetCommand.DescribeInstall` (Task 4), `IDialogService`, `IInfoBarService`, `IStatusService`, `INavigationAware`, `ViewModelBase`.
- Produces: `AppTileViewModel` with `Model`, `Name`, `Publisher`, `Description`, `Initial`, `AccentColor`, `State` (`AppTileState`), `IsInstallable`, `InstallCommand`; `AppTileState` (enum: `Checking`, `Unknown`, `NotInstalled`, `Queued`, `Installing`, `Installed`, `NeedsRestart`, `Failed`); `DownloadsViewModel` with `Tiles`, `Filtered`, `Categories`, `SelectedCategory`, `SearchText`, `StatusText`, `IsBusy`.

- [ ] **Step 1: Write `AppTileViewModel.cs`**

In the style of `UninstallerViewModel`'s `InstalledAppEntryViewModel`: an
`ObservableObject` wrapping one `InstallableApp`, exposing `Name`, `Publisher`, `Description`,
`AccentColor`, and `Initial` (the first letter of `Name`, uppercased, for the avatar square).

- `AppTileState State` is an `[ObservableProperty]`; `StateText` is a computed string for the
  caption under the button: "Installed", "Installed — restart to finish", "Queued", "Installing…",
  "Could not install", "Not installed", or "" while checking.
- `IsInstallable` is true when `State` is `NotInstalled` or `Failed` — the two states where the
  Install button is live. It must be `NotifyPropertyChangedFor`-linked from `State`.
- `InstallCommand` is a `[RelayCommand]` that does nothing; the page view model owns the queue and
  sets the command's action (pass an `Action<AppTileViewModel>` into the constructor, as
  `InstalledAppEntryViewModel` takes its `selectionChanged` callback).

- [ ] **Step 2: Write `DownloadsViewModel.cs`**

`sealed partial class DownloadsViewModel : ViewModelBase, INavigationAware`, constructor-injected
with `IAppInstallService apps, IDialogService dialogs, IInfoBarService infoBar, IStatusService
status, ILogger<DownloadsViewModel> logger`, and `Title = "Downloads"`.

- `OnNavigatedTo` kicks off `LoadAsync()` the way `UninstallerViewModel` does.
- **`LoadAsync`**: call `FindInstalledIdsAsync`; for each tile set `State` from the set —
  `Installed` when listed, `NotInstalled` when not, and `Unknown` when the service returned
  `null`. When it is `null`, say so **once, plainly**, on the status line ("Could not ask winget
  what is installed — is App Installer installed?") rather than on all twenty tiles; each tile
  reads "Unknown" beside its button. Never let `null` fall through to "not installed".
- **Filtering**: `Categories` is "All" plus one entry per `InstallableAppCategory`;
  `SelectedCategory` and `SearchText` drive `ApplyFilter()`, which is a straight port of
  `UninstallerViewModel.ApplyFilter` (match on `Name` and `Publisher`, ordinal-current-culture,
  ignore case), and is called from `OnSearchTextChanged` and `OnSelectedCategoryChanged`.
- **The queue**: `InstallCommand(AppTileViewModel tile)` confirms first with
  `_dialogs.ConfirmAsync($"Install {tile.Name}?", ..., "Install", "Cancel")` where the message
  names the app and its publisher and ends with the plain warning that it cannot be undone — in
  the spirit of `UninstallerViewModel`'s "This cannot be undone." Then:

  - **Dry run** (`_apps.IsDryRun`): do not queue anything. Show the command in the info bar
    (`WingetCommand.DescribeInstall(tile.Model)`) and leave the tile's state alone.
  - Otherwise add the tile to a `Queue<AppTileViewModel>`, set its state to `Queued`, and start
    the pump if one is not already running.

- **The pump** (`RunQueueAsync`) loops while the queue is non-empty: dequeue, set `Installing`,
  install inside `using (_status.Begin($"Installing {tile.Name}"))`, then set the tile from the
  outcome — `Installed`, `NeedsRestart`, or `AlreadyInstalled` all end as *not* installable.
  Catch per tile: log the failure with `ILogger` and set `Failed`, then **continue with the next
  tile** (one failure must not cancel the queue). Afterwards re-run the read-back once and report a
  summary in the info bar, the way `UninstallerViewModel` reports "Uninstalled N, failed M",
  carrying winget's own reason for each failure. There is **no separate log window** (spec Call 8):
  the failure reason lives in the info bar, the running job on the status line, and everything in
  the existing file log.
- `IsBusy` is true while the pump runs; `StatusText` says which app is being installed.

- [ ] **Step 3: Register the view model**

In `src/AkariDash.App/App.xaml.cs`, with the other page view models (around lines 170-181):

```csharp
builder.Services.AddTransient<DownloadsViewModel>();
```

- [ ] **Step 4: Build and verify it compiles**

Run: `dotnet build AkariDash.slnx -c Phase1`
Expected: build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/AkariDash.App/ViewModels/AppTileViewModel.cs src/AkariDash.App/ViewModels/DownloadsViewModel.cs src/AkariDash.App/App.xaml.cs
git commit -m "Downloads: the page view model, its tiles and the install queue"
```

---

### Task 6: The page

**Files:**
- Modify: `src/AkariDash.App/Views/DownloadsPage.xaml` (replace the placeholder)
- Modify: `src/AkariDash.App/Views/DownloadsPage.xaml.cs`
- Modify: `src/AkariDash.App/ShellPages.cs:29` (subtitle)

**Interfaces:**
- Consumes: `DownloadsViewModel`, `AppTileViewModel` (Task 5).
- Produces: nothing later tasks use.

- [ ] **Step 1: Write the page**

Replace `WorkInProgressView` in `DownloadsPage.xaml` with, top to bottom:

- A header row: the "Downloads" title, the status line (`StatusText`) and a `ProgressRing` bound
  to `IsBusy`, exactly as `UwpAppsPage.xaml:194-195` does.
- A search `TextBox` bound to `SearchText` (`Mode=TwoWay`, `UpdateSourceTrigger=PropertyChanged`).
- The category chips: an `ItemsRepeater` (or a horizontal `StackPanel`) over `Categories` whose
  item is a `ToggleButton` in the `TunerChipViewModel` style, one chip per category.
- The tiles: an `ItemsRepeater` over `Filtered` with a `UniformGridLayout`
  (`MinItemWidth`/`MinItemHeight` sized for three across at the default window width), as
  `HomePage.xaml:153-195` does for its quick links. Each item is a card with a rounded accent
  square holding `Initial` on `AccentColor`, the name, the publisher, the description, and the
  Install button with `StateText` beside it; a `ProgressRing` on the card is active while
  `State == Installing`.

Keep the `x:Class`, the `views` namespace declaration, and set `DataContext` in the code-behind.

- [ ] **Step 2: Write the code-behind**

```csharp
public sealed partial class DownloadsPage : Page
{
    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage(DownloadsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 3: Give the nav item a real subtitle**

In `src/AkariDash.App/ShellPages.cs`, replace `Subtitle: "Work in progress"` on the Downloads
line with `Subtitle: "Install the apps a gaming PC needs — launchers, runtimes and tools"`.

- [ ] **Step 4: Build and launch the page**

Run: `dotnet build AkariDash.slnx -c Phase1`
Expected: build succeeded, 0 errors.

Then run the Phase 1 build and open **Downloads** in the sidebar. Expected: the grid of tiles with
avatars, chips and search; Install buttons act as though they were pressed but nothing is
installed and the info bar shows the `winget install …` line it would have run. No page crashes on
navigation, and search and the chips both narrow the grid.

- [ ] **Step 5: Run the whole test suite to verify nothing regressed**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj`
Expected: PASS, no failures.

- [ ] **Step 6: Commit**

```bash
git add src/AkariDash.App/Views/DownloadsPage.xaml src/AkariDash.App/Views/DownloadsPage.xaml.cs src/AkariDash.App/ShellPages.cs
git commit -m "Downloads: the page itself, tiles with a search box and category chips"
```

---

### Task 7: The VM build and its check

**Files:**
- Create: `vm-build/VM-CHECK-DOWNLOADS.md`

**Interfaces:**
- Consumes: everything above.
- Produces: the built app in `vm-build/AkariDash-Debug` and the steps to test it.

- [ ] **Step 1: Publish the Debug build for the VM**

Run: `dotnet publish src/AkariDash.App/AkariDash.App.csproj -c Debug -o vm-build/AkariDash-Debug`
Expected: the folder holds `Akari Dash.exe` beside the files it already had, refreshed.

- [ ] **Step 2: Write `vm-build/VM-CHECK-DOWNLOADS.md`**

In the style of `vm-build/VM-CHECK-UWP-APPS.md`: a title, the reminder that this is **VM only**
and the path to the exe (`vm-build\AkariDash-Debug\Akari Dash.exe`), then numbered steps that each
say what to do and what to expect. Cover at least:

1. The nav item is **Downloads** with the new subtitle; the page shows the grid with avatars, the
   search box and the chips.
2. The filter chips narrow the grid to one category each, and search narrows by name and publisher.
3. **Installed apps are shown as installed before you touch anything** — check the Visual C++
   redistributable and the .NET Desktop Runtime, and confirm against an elevated PowerShell
   (`winget list --id Microsoft.DotNet.DesktopRuntime.8 --exact`) that the page agrees.
4. Click Install on **7-Zip**: the confirm appears, naming 7-Zip and saying it cannot be undone.
   Cancel: nothing happens. Click Install again and confirm: the tile shows progress, the status
   line names it, and it ends as installed. Confirm independently that 7-Zip is now in
   Add/Remove Programs, and that the tile reads as installed after **Refresh**.
5. Click Install on **Steam** (a large download): while it runs, queue **Discord** and **VLC**.
   All three end as installed and the status line names each in turn, in the order they were clicked.
6. A runtime: if the VM does not already have it, remove then reinstall `.NET Desktop Runtime 8` and
   check that a "restart to finish" result shows as installed with the restart note, not as an error.
7. A failure: put the VM's network offline and install something not already present. The tile
   reads as a failure with a readable reason, and **the rest of the page still works**.
8. Confirm the whole-page read-back when winget cannot be asked: temporarily rename
   `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe` (restore it afterwards). The tiles read as
   unknown rather than as "not installed".
9. **The truncation check.** In an elevated PowerShell, run
   `winget list --id Microsoft.VCRedist.2015+.x64 --exact` and separately capture
   `winget list | Out-String` redirected to a file. Confirm the redirected output is **not**
   truncating the Id column; if it is, note it — the page will then show runtimes as unknown
   instead of guessing, and that needs fixing properly.

- [ ] **Step 3: Commit**

```bash
git add vm-build/VM-CHECK-DOWNLOADS.md
git commit -m "Downloads: what to check in the VM"
```

---

## Notes for the executor

- `vm-build/` is in the repository and is committed, but `AkariDash-Debug/`'s binary output is
  build output; add only the Markdown to the commit unless the repo already tracks those binaries
  (check `git status` before `git add`).
- The Phase 1 build is the safe one to launch outside the VM, but note that **no existing Action is
  gated by Phase 1** — the inert Install button is new behaviour introduced by this plan, per
  Call 5 in the spec. If a later task turns out to need Actions gated more broadly, that is a
  separate change, not part of this plan.
