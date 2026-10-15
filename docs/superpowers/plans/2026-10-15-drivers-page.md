# Drivers Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Drivers placeholder into a page that detects the graphics card(s), finds the vendor's current driver, and installs it — plain, configured, or debloated — as an Action.

**Architecture:** Everything that decides anything lives in `AkariDash.Core` as data and pure functions (the rows, the vendor lookups and their parsers, the install command lines, the debloat strip lists, the settings bundle, and the version comparison) and is unit-tested there. A thin Windows service in `AkariDash.App` does the network, process and registry work behind `IDriverService`. The page and its view model follow the Downloads page's house pattern. **This plan does not include the DDU clean or the guided "Reset and reinstall" card** — per the design they are the second step, and they depend on the rows built here.

**Tech Stack:** .NET 10, WinUI 3 (Windows App SDK), CommunityToolkit.Mvvm, Microsoft.Extensions DI/logging, `System.Text.Json`, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-15-drivers-page-design.md`

## Global Constraints

- **`AkariDash.Tests` references `AkariDash.Core` and `AkariDash.Framework` only — never `AkariDash.App`.** All unit-testable logic therefore belongs in Core; the service, the page and the view model are verified by building and by the VM check.
- **Target framework is `net10.0-windows10.0.26100.0`, `Nullable` enabled, `ImplicitUsings` enabled.**
- **Configurations are `Debug;Release;Phase1`.** `Phase1` sets `BuildInfo.IsDryRunOnly`. In Phase 1 no driver is downloaded, no installer runs, and no settings are written; the page reports the command lines it would have run.
- **An install is an Action (ADR-0004):** not undoable, warns first — naming the card — reports progress, logs, and reads back what the machine has afterwards. It is never shown as a Tweak.
- **Never test on real hardware.** Everything lands in `vm-build\AkariDash-Debug` and is tested in a VM.
- **No PowerShell, and no `.ps1` written to disk.** Network, process, registry and archive work is .NET/Win32.
- **Vendor-aware.** The card(s) come from `IMachine.GpuVendors()` (registered at `src/AkariDash.App/App.xaml.cs:136`; reads are always live, even in Phase 1). A vendor that is not present gets no rows.
- **The source being ported** is `C:\Users\isleap\Documents\GitHub\AkariOS-Ultimate\Tweaks\Graphics.ps1`; line ranges below are cited so values are transcribed, not invented.
- **Commits:** one per task, message style as the repo uses it (`Area: what changed`).

## Review Focus

Carried forward from the spec; each line is pinned in the task that owns the code — by a unit test where the code is in `AkariDash.Core`, and by the VM check (Task 10) where it is in `AkariDash.App`.

1. **A vendor endpoint answering with a consent or error page instead of a driver.** The parser must return `null` ("could not read"), never a plausible-looking wrong URL. (Task 2)
2. **A version that cannot be compared** — `566.36` against AMD's `25.3.1`, or a `null` from a failed read. The comparison must say `Unknown`, never "up to date". (Task 5)
3. **A machine with no PCI graphics card** (a fresh VM, or Basic Display Adapter only), so `GpuVendors()` is empty. The page must say there is no card, not show every vendor's rows. (Tasks 1, 8, 10)
4. **A file in the drop folder that is not a recognised vendor installer.** The debloat must refuse it and delete nothing, rather than applying one vendor's strip list to another's file. (Task 4)
5. **A configure bundle whose XML is malformed, or whose registry path is rooted or contains `..`.** The bundle must not be applied. (Task 6)

---

### Task 1: The driver rows as data

**Files:**
- Create: `src/AkariDash.Core/Drivers/DriverRow.cs`
- Create: `src/AkariDash.Core/Drivers/DriverRows.cs`
- Test: `src/AkariDash.Tests/DriverRowsTests.cs`

**Interfaces:**
- Consumes: `GpuVendor` (`AkariDash.Core.Machine`).
- Produces: `DriverStepKind` (enum: `Install`, `InstallAndConfigure`, `Debloat`); `DriverRisk` (enum: `Safe`, `Caution`, `Advanced`); `DriverRow(string Id, GpuVendor Vendor, DriverStepKind Kind, string Title, string Description, DriverRisk Risk)`; `DriverRows.All` (`IReadOnlyList<DriverRow>`); `DriverRows.For(GpuVendor vendor)` (`IReadOnlyList<DriverRow>`).

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverRowsTests
{
    [Fact]
    public void Every_row_is_listed_once()
    {
        Assert.Equal(
            DriverRows.All.Count,
            DriverRows.All.Select(row => row.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_row_is_complete()
    {
        Assert.All(DriverRows.All, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Id));
            Assert.False(string.IsNullOrWhiteSpace(row.Title));
            Assert.False(string.IsNullOrWhiteSpace(row.Description));
            Assert.EndsWith(".", row.Description);          // one sentence, in RestorableApps' voice
            Assert.Matches("^[a-z0-9.-]+$", row.Id);        // e.g. nvidia.install-and-configure
            Assert.StartsWith(row.Vendor.ToString().ToLowerInvariant(), row.Id);
        });
    }

    [Fact]
    public void Every_vendor_has_the_three_rows()
    {
        Assert.All(Enum.GetValues<GpuVendor>(), vendor =>
        {
            var rows = DriverRows.For(vendor);
            Assert.Equal(3, rows.Count);
            Assert.All(rows, row => Assert.Equal(vendor, row.Vendor));
            Assert.Equal(
                [DriverStepKind.Install, DriverStepKind.InstallAndConfigure, DriverStepKind.Debloat],
                rows.Select(row => row.Kind));
        });
    }

    // Review Focus 3: an unknown vendor (or no card) must yield nothing, not every row.
    [Fact]
    public void A_vendor_with_no_rows_yields_none() =>
        Assert.Empty(DriverRows.For((GpuVendor)999));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverRowsTests"`
Expected: FAIL to compile — `DriverRow` does not exist.

- [ ] **Step 3: Write `DriverRow.cs`**

```csharp
using AkariDash.Core.Machine;

namespace AkariDash.Core.Drivers;

/// <summary>What kind of work a driver row does.</summary>
public enum DriverStepKind
{
    Install,
    InstallAndConfigure,
    Debloat,
}

/// <summary>How much care a row needs before it is pressed.</summary>
public enum DriverRisk
{
    Safe,
    Caution,
    Advanced,
}

/// <summary>
/// One row on the Drivers page: which card it is for, what it does, and the one line that
/// tells a person what they are about to do.
/// </summary>
public sealed record DriverRow(
    string Id,
    GpuVendor Vendor,
    DriverStepKind Kind,
    string Title,
    string Description,
    DriverRisk Risk);
```

- [ ] **Step 4: Write `DriverRows.cs`**

A static class in the style of `src/AkariDash.Core/Apps/InstallableApps.cs`, with
`public static IReadOnlyList<DriverRow> All { get; } = [ ... ];` and
`public static IReadOnlyList<DriverRow> For(GpuVendor vendor) => All.Where(row => row.Vendor == vendor).ToList();`.

Three rows per vendor, ids `nvidia.install`, `nvidia.install-and-configure`, `nvidia.debloat` (and
`amd.…`, `intel.…`). Titles in the page's voice: *"Install the latest NVIDIA driver"*, *"Install and
configure"*, *"Install a driver you downloaded"*. Risk: `Install` is `Caution`; `InstallAndConfigure`
is `Caution`; `Debloat` is `Advanced`.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverRowsTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Commit**

```bash
git add src/AkariDash.Core/Drivers/DriverRow.cs src/AkariDash.Core/Drivers/DriverRows.cs src/AkariDash.Tests/DriverRowsTests.cs
git commit -m "Drivers: the driver rows, as data"
```

---

### Task 2: Reading each vendor's current driver

**Files:**
- Create: `src/AkariDash.Core/Drivers/AvailableDriver.cs`
- Create: `src/AkariDash.Core/Drivers/DriverLookup.cs`
- Test: `src/AkariDash.Tests/DriverLookupTests.cs`

**Interfaces:**
- Consumes: `GpuVendor` (Task 1's namespace sibling).
- Produces: `AvailableDriver(string Version, Uri DownloadUrl)`; `DriverLookup.Endpoint(GpuVendor vendor)` → `Uri`; `DriverLookup.Parse(GpuVendor vendor, string body)` → `AvailableDriver?`; `DriverLookup.NvidiaEndpoint` / `DriverLookup.AmdEndpoint` / `DriverLookup.IntelEndpoint` → `Uri`.

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverLookupTests
{
    // A trimmed copy of a real AjaxDriverService answer; the executor captures the live one in
    // Step 5 and pastes it here. Version and URL are checked against each other, so a newer
    // driver does not fail the test.
    private const string NvidiaAnswer = """
        {"IDS":[{"downloadInfo":{"Version":"566.36",
        "DownloadURL":"https://international.download.nvidia.com/Windows/566.36/566.36-desktop-win10-win11-64bit-international-dch-whql.exe"},"Success":"1"}]}
        """;

    [Fact]
    public void Nvidia_reads_the_version_and_its_download_url()
    {
        var driver = DriverLookup.Parse(GpuVendor.Nvidia, NvidiaAnswer);

        Assert.NotNull(driver);
        Assert.Equal("566.36", driver.Version);
        Assert.Contains(driver.Version, driver.DownloadUrl.AbsoluteUri);  // the URL names the version
        Assert.Equal("https", driver.DownloadUrl.Scheme);
    }

    // Review Focus 1: a consent page, an empty body, or an error must read as "could not ask".
    [Theory]
    [InlineData("<html><body>Access denied</body></html>")]
    [InlineData("")]
    [InlineData("""{"IDS":[]}""")]
    public void Nvidia_reads_nothing_from_anything_else(string body) =>
        Assert.Null(DriverLookup.Parse(GpuVendor.Nvidia, body));

    private const string AmdAnswer = """
        <a href="https://drivers.amd.com/drivers/installer/25.30/whql/amd-software-adrenalin-edition-25.3.1-minimalsetup-250307_web.exe">Download</a>
        """;

    [Fact]
    public void Amd_reads_the_minimalsetup_link()
    {
        var driver = DriverLookup.Parse(GpuVendor.Amd, AmdAnswer);

        Assert.NotNull(driver);
        Assert.Equal("25.3.1", driver.Version);                            // read out of the file name
        Assert.EndsWith("_web.exe", driver.DownloadUrl.AbsoluteUri);
    }

    // Review Focus 1: a link on a lookalike host is not AMD's installer.
    [Fact]
    public void Amd_ignores_a_link_that_is_not_on_drivers_amd_com() =>
        Assert.Null(DriverLookup.Parse(GpuVendor.Amd,
            """<a href="https://example.com/amd-software-adrenalin-edition-25.3.1-minimalsetup-1_web.exe">x</a>"""));

    // Intel has no feed we can parse; the row hands off to the page instead.
    [Fact]
    public void Intel_has_a_page_but_no_parse() =>
        Assert.Null(DriverLookup.Parse(GpuVendor.Intel, "<html></html>"));

    [Fact]
    public void Every_vendor_has_an_https_endpoint() =>
        Assert.All(Enum.GetValues<GpuVendor>(),
            vendor => Assert.Equal("https", DriverLookup.Endpoint(vendor).Scheme));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverLookupTests"`
Expected: FAIL to compile — `DriverLookup` does not exist.

- [ ] **Step 3: Write `AvailableDriver.cs`**

```csharp
namespace AkariDash.Core.Drivers;

/// <summary>The driver a vendor says is current for a card, as the vendor itself reports it.</summary>
public sealed record AvailableDriver(string Version, Uri DownloadUrl);
```

- [ ] **Step 4: Write `DriverLookup.cs`**

A static class holding three things, all data: the endpoints, the parsers, and nothing else.

- `Endpoint(vendor)`: NVIDIA → **exactly** the URL at `Graphics.ps1:207`; AMD → `https://www.amd.com/en/support/download/drivers.html` (`Graphics.ps1:225`); Intel → the search page at `Graphics.ps1:241`. All `https`.
- `ParseNvidia(json)`: `System.Text.Json`; read `IDS[0].downloadInfo.Version` and build the download
  URL exactly as `Graphics.ps1:213` does —
  `https://international.download.nvidia.com/Windows/{version}/{version}-desktop-win10-win11-64bit-international-dch-whql.exe`.
  Return `null` when the body is not JSON, `IDS` is empty, or the version is blank. **Do not** catch
  and swallow into a guess: `JsonException` and `UriFormatException` both become `null`.
- `ParseAmd(html)`: find the first `href` matching
  `drivers\.amd\.com/drivers/installer/.*/whql/amd-software-adrenalin-edition-.*-minimalsetup-.*_web\.exe`
  (`Graphics.ps1:227`) — as a `[GeneratedRegex]` partial or a compiled `Regex`. The version is the
  text between `adrenalin-edition-` and `-minimalsetup` in the file name; return `null` when there is
  no match. The match must be on the **host** `drivers.amd.com`, not a substring anywhere in the URL.
- `Parse(vendor, body)`: dispatch to the two parsers; `Intel` always returns `null`.
- **The NVIDIA `psid`/`pfid` in the endpoint identify a product family.** Carry them as named
  constants in this file with a comment saying what they are; Step 5 is where they are confirmed.

- [ ] **Step 5: Verify both live endpoints, and capture the NVIDIA answer**

Run each and confirm the parser against reality (this is the design's "verify every endpoint" item):

```powershell
Invoke-WebRequest "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=120&pfid=929&osID=57&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1" -UseBasicParsing | Select-Object -ExpandProperty Content
Invoke-WebRequest "https://www.amd.com/en/support/download/drivers.html" -UseBasicParsing | Select-Object -ExpandProperty Links | Where-Object { $_.href -match "minimalsetup" } | Select-Object href
```

Expected: the NVIDIA answer is JSON with an `IDS[0].downloadInfo.Version`, and the AMD page still
carries a `minimalsetup ... _web.exe` link. **Paste a trimmed copy of the NVIDIA answer into the test
as `NvidiaAnswer`** if it differs from the fixture in shape. If the AMD link pattern has changed,
update the regex here **and** the pattern recorded in the spec, so the two stay together.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverLookupTests"`
Expected: PASS, 8 tests.

- [ ] **Step 7: Commit**

```bash
git add src/AkariDash.Core/Drivers/AvailableDriver.cs src/AkariDash.Core/Drivers/DriverLookup.cs src/AkariDash.Tests/DriverLookupTests.cs
git commit -m "Drivers: reading NVIDIA's and AMD's current driver"
```

---

### Task 3: The install command lines

**Files:**
- Create: `src/AkariDash.Core/Drivers/DriverInstallCommand.cs`
- Test: `src/AkariDash.Tests/DriverInstallCommandTests.cs`

**Interfaces:**
- Consumes: `DriverRow` / `DriverStepKind` / `DriverRisk` (Task 1), `GpuVendor`.
- Produces: `DriverInstallCommand.Install(DriverRow row, string installerPath)` → `IReadOnlyList<string>`; `DriverInstallCommand.IsUnattended(GpuVendor vendor)` → `bool`; `DriverInstallCommand.Describe(string executable, IReadOnlyList<string> arguments)` → `string`.

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverInstallCommandTests
{
    private static DriverRow Row(GpuVendor vendor, DriverStepKind kind) =>
        new($"{vendor}".ToLowerInvariant() + ".x", vendor, kind, "Title", "Description.", DriverRisk.Caution);

    // Graphics.ps1:269 - NVIDIA's own flags, kept exactly.
    [Fact]
    public void Nvidia_installs_silently_without_rebooting_or_asking()
    {
        var arguments = DriverInstallCommand.Install(Row(GpuVendor.Nvidia, DriverStepKind.Install), "setup.exe");

        Assert.Contains("-s", arguments);
        Assert.Contains("-noreboot", arguments);
        Assert.Contains("-noeula", arguments);
        Assert.DoesNotContain("-clean", arguments);           // the clean install is the configured row
    }

    [Fact]
    public void The_configured_row_asks_NVIDIA_for_a_clean_install() =>
        Assert.Contains("-clean",
            DriverInstallCommand.Install(Row(GpuVendor.Nvidia, DriverStepKind.InstallAndConfigure), "setup.exe"));

    // If we cannot install a vendor unattended, we must not emit a bare path that opens a wizard
    // while the page claims to be working: no arguments, and IsUnattended says so.
    [Fact]
    public void Only_NVIDIA_can_be_installed_unattended()
    {
        Assert.True(DriverInstallCommand.IsUnattended(GpuVendor.Nvidia));
        Assert.False(DriverInstallCommand.IsUnattended(GpuVendor.Amd));
        Assert.False(DriverInstallCommand.IsUnattended(GpuVendor.Intel));
        Assert.Empty(DriverInstallCommand.Install(Row(GpuVendor.Amd, DriverStepKind.Install), "setup.exe"));
        Assert.Empty(DriverInstallCommand.Install(Row(GpuVendor.Intel, DriverStepKind.Install), "setup.exe"));
    }

    [Fact]
    public void Describe_is_the_line_a_person_would_type() =>
        Assert.Equal(
            "C:\\Temp\\setup.exe -s -noreboot",
            DriverInstallCommand.Describe("C:\\Temp\\setup.exe", ["-s", "-noreboot"]));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverInstallCommandTests"`
Expected: FAIL to compile — `DriverInstallCommand` does not exist.

- [ ] **Step 3: Write `DriverInstallCommand.cs`**

A static class holding the argument sets, in the style of `WingetCommand`. NVIDIA's are proven
(`Graphics.ps1:269`) and go in exactly:

- `InstallAndConfigure` + NVIDIA → `["-s", "-noreboot", "-noeula", "-clean"]`
- `Install` + NVIDIA → `["-s", "-noreboot", "-noeula"]`
- AMD and Intel → an empty list, and `IsUnattended` is `false` for both. AkariOS launches AMD's web
  installer with no arguments at all (`Graphics.ps1:366`) and Intel's through a file picker
  (`Graphics.ps1:423`); neither is unattended. The row will hand off to the installer instead
  (Task 8), which is the honest outcome rather than a silent-install claim we cannot keep.

`Describe` joins the executable and its arguments with a space, quoting an argument that contains a
space, and is what the Phase 1 build reports.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverInstallCommandTests"`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AkariDash.Core/Drivers/DriverInstallCommand.cs src/AkariDash.Tests/DriverInstallCommandTests.cs
git commit -m "Drivers: the install command lines, silent where the vendor allows it"
```

---

### Task 4: The debloat plan, and the drop folder

**Files:**
- Create: `src/AkariDash.Core/Drivers/DriverDebloat.cs`
- Create: `src/AkariDash.Core/Drivers/DriverInbox.cs`
- Test: `src/AkariDash.Tests/DriverDebloatTests.cs`

**Interfaces:**
- Consumes: `GpuVendor` (Task 1).
- Produces: `DebloatPlan(IReadOnlyList<string> RemoveAfterExtracting)`; `DriverDebloat.Plan(GpuVendor vendor, string installerFileName)` → `DebloatPlan?`; `DriverInbox.Folder` → `string`; `DriverInbox.Installers(IEnumerable<string> filePaths)` → `IReadOnlyList<string>`.

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

public class DriverDebloatTests
{
    [Fact]
    public void An_nvidia_installer_gets_NVIDIA_s_strip_list()
    {
        var plan = DriverDebloat.Plan(GpuVendor.Nvidia, "566.36-desktop-win10-win11-64bit-international-dch-whql.exe");

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.RemoveAfterExtracting);
    }

    // Review Focus 4: another vendor's file, or anything unknown, is refused outright.
    [Theory]
    [InlineData(GpuVendor.Nvidia, "amd-software-adrenalin-edition-25.3.1-minimalsetup-1_web.exe")]
    [InlineData(GpuVendor.Amd, "566.36-desktop-win10-win11-64bit-international-dch-whql.exe")]
    [InlineData(GpuVendor.Nvidia, "totally-unrelated-installer.exe")]
    [InlineData(GpuVendor.Nvidia, "readme.txt")]
    public void A_file_that_is_not_that_vendor_s_installer_is_refused(GpuVendor vendor, string fileName) =>
        Assert.Null(DriverDebloat.Plan(vendor, fileName));

    // Nothing in a strip list may reach outside the folder we extracted into.
    [Fact]
    public void No_strip_entry_leaves_the_extraction_folder()
    {
        Assert.All(Enum.GetValues<GpuVendor>(), vendor =>
        {
            var plan = DriverDebloat.Plan(vendor, DriverDebloat.InstallerNameFor(vendor));
            if (plan is null)
            {
                return;
            }

            Assert.All(plan.RemoveAfterExtracting, entry =>
            {
                Assert.False(Path.IsPathRooted(entry));
                Assert.DoesNotContain("..", entry);
            });
        });
    }

    [Fact]
    public void The_inbox_lists_only_executables_and_never_folders()
    {
        var found = DriverInbox.Installers(
            ["C:\\x\\driver.exe", "C:\\x\\notes.txt", "C:\\x\\sub", "C:\\x\\setup.EXE"]);

        Assert.Equal(2, found.Count);
        Assert.All(found, path => Assert.EndsWith(".exe", path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_inbox_lives_under_the_user_s_Documents() =>
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            DriverInbox.Folder,
            StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverDebloatTests"`
Expected: FAIL to compile — `DriverDebloat` does not exist.

- [ ] **Step 3: Write `DriverDebloat.cs`**

A static class holding:

- `InstallerNameFor(vendor)` — the file name each vendor's own installer has (used by the test above
  and by the row's wording).
- `Plan(vendor, installerFileName)` — recognise the file by that vendor's **own naming** (NVIDIA
  installers end `-dch-whql.exe`; AMD's are `amd-software-adrenalin-edition-…-minimalsetup-…_web.exe`;
  Intel's are `Intel…`, e.g. `IntelGraphicsSoftware…exe`), and return that vendor's plan; return
  `null` for anything else **including another vendor's installer**.
- The strip lists come from AkariOS's debloat entry, `Graphics.ps1:430-560` (NVIDIA's extracted-folder
  removals, AMD's, Intel's) — **transcribe the entries that are still safe on a current driver, and
  leave out anything that removes the driver's own core**. Each entry is a path **relative to the
  extraction folder**; a rooted path or one containing `..` is a bug, not a feature.

Where AkariOS extracted with a shipped `7-Zip` (`Graphics.ps1:251-267`), the driver service uses the
vendor installer's own extraction switch, or a managed archive library if it has none — no `7z.exe`
is shipped or installed as a side effect (design Call 4). The extraction **destination** is decided by
the service (Task 7); this class only says what to remove afterwards.

- [ ] **Step 4: Write `DriverInbox.cs`**

```csharp
namespace AkariDash.Core.Drivers;

/// <summary>
/// The folder a person puts a downloaded driver installer in. There is no file picker: Akari-Dash
/// runs elevated, where the pickers do not open (see SystemCleanupViewModels' Drivers tab, which
/// works around the same thing with a fixed folder).
/// </summary>
public static class DriverInbox
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Akari-Dash", "Downloaded drivers");

    /// <summary>The installer files in the folder, newest first; folders and everything else are ignored.</summary>
    public static IReadOnlyList<string> Installers(IEnumerable<string> filePaths) =>
        filePaths
            .Where(path => string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverDebloatTests"`
Expected: PASS, 8 tests.

- [ ] **Step 6: Commit**

```bash
git add src/AkariDash.Core/Drivers/DriverDebloat.cs src/AkariDash.Core/Drivers/DriverInbox.cs src/AkariDash.Tests/DriverDebloatTests.cs
git commit -m "Drivers: what a debloat removes, and the folder it reads from"
```

---

### Task 5: Comparing the installed driver with the available one

**Files:**
- Create: `src/AkariDash.Core/Drivers/DriverVersion.cs`
- Test: `src/AkariDash.Tests/DriverVersionTests.cs`

**Interfaces:**
- Consumes: `AvailableDriver` (Task 2).
- Produces: `DriverVersionState` (enum: `Unknown`, `UpToDate`, `UpdateAvailable`); `DriverVersion.State(string? installed, AvailableDriver? available)` → `DriverVersionState`.

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Drivers;
using Xunit;

namespace AkariDash.Tests;

public class DriverVersionTests
{
    private static AvailableDriver Driver(string version) => new(version, new Uri("https://example.com/d.exe"));

    [Fact]
    public void An_older_installed_driver_has_an_update() =>
        Assert.Equal(DriverVersionState.UpdateAvailable, DriverVersion.State("566.36", Driver("572.16")));

    [Fact]
    public void The_same_driver_is_up_to_date() =>
        Assert.Equal(DriverVersionState.UpToDate, DriverVersion.State("566.36", Driver("566.36")));

    [Fact]
    public void A_newer_installed_driver_is_not_an_update() =>
        Assert.Equal(DriverVersionState.UpToDate, DriverVersion.State("572.16", Driver("566.36")));

    // AMD's numbering has different-length parts; 25.3 is older than 25.3.1.
    [Fact]
    public void A_shorter_installed_version_is_older() =>
        Assert.Equal(DriverVersionState.UpdateAvailable, DriverVersion.State("25.3", Driver("25.3.1")));

    // Review Focus 2: what we cannot compare we must not call either way.
    [Theory]
    [InlineData(null, "566.36")]
    [InlineData("566.36", null)]
    [InlineData("", "566.36")]
    [InlineData("unknown", "566.36")]
    [InlineData("566.36", "")]
    [InlineData("566.36", "unknown")]
    public void Anything_we_cannot_read_is_unknown(string? installed, string? available) =>
        Assert.Equal(DriverVersionState.Unknown, DriverVersion.State(installed, available is null ? null : Driver(available)));

    [Fact]
    public void Two_unreadable_versions_are_unknown() =>
        Assert.Equal(DriverVersionState.Unknown, DriverVersion.State(null, null));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverVersionTests"`
Expected: FAIL to compile — `DriverVersion` does not exist.

- [ ] **Step 3: Write `DriverVersion.cs`**

Compare dotted numeric versions by splitting on `.`, parsing each part with
`int.TryParse(..., CultureInfo.InvariantCulture)`, padding the shorter one with zeros, and comparing
part by part. A non-numeric part, a blank string, or a `null` on either side makes the whole answer
`Unknown` — never `UpToDate`, because "we could not tell" and "it is current" are different things a
person will act on differently.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverVersionTests"`
Expected: PASS, 11 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AkariDash.Core/Drivers/DriverVersion.cs src/AkariDash.Tests/DriverVersionTests.cs
git commit -m "Drivers: is the driver on this card out of date, and saying so only when we know"
```

---

### Task 6: The settings a configured install applies

**Files:**
- Create: `src/AkariDash.Core/Drivers/DriverSettings.cs`
- Test: `src/AkariDash.Tests/DriverSettingsTests.cs`

**Interfaces:**
- Consumes: `GpuVendor`.
- Produces: `DriverSettingRoot` (enum: `Hkcu`, `HklmServices`, `GraphicsClass`); `DriverSettingKind` (enum: `Dword`, `String`, `Binary`); `DriverSetting(DriverSettingRoot Root, string KeyPath, string Name, DriverSettingKind Kind, string Value)`; `DriverSettings.For(GpuVendor vendor)` → `IReadOnlyList<DriverSetting>`; `DriverSettings.NvidiaProfileXml` → `string`.

- [ ] **Step 1: Write the failing test**

```csharp
using AkariDash.Core.Drivers;
using AkariDash.Core.Machine;
using System.Xml.Linq;
using Xunit;

namespace AkariDash.Tests;

public class DriverSettingsTests
{
    [Fact]
    public void Every_vendor_has_settings()
    {
        Assert.All(Enum.GetValues<GpuVendor>(), vendor => Assert.NotEmpty(DriverSettings.For(vendor)));
    }

    [Fact]
    public void Every_setting_is_complete()
    {
        Assert.All(Enum.GetValues<GpuVendor>().SelectMany(DriverSettings.For), setting =>
        {
            Assert.True(Enum.IsDefined(setting.Root));
            Assert.True(Enum.IsDefined(setting.Kind));
            Assert.False(string.IsNullOrWhiteSpace(setting.KeyPath));
            Assert.False(string.IsNullOrWhiteSpace(setting.Name));
            Assert.False(string.IsNullOrWhiteSpace(setting.Value));
        });
    }

    // Each value is written with the type it declares, so the service never has to guess one.
    [Fact]
    public void Every_value_matches_the_type_it_declares()
    {
        Assert.All(Enum.GetValues<GpuVendor>().SelectMany(DriverSettings.For), setting =>
        {
            switch (setting.Kind)
            {
                case DriverSettingKind.Dword:
                    Assert.Matches(@"^[0-9]+$", setting.Value);
                    break;
                case DriverSettingKind.Binary:
                    Assert.Matches(@"^([0-9A-Fa-f]{2})+$", setting.Value);   // whole bytes, never a half
                    break;
                case DriverSettingKind.String:
                    Assert.DoesNotContain('\0', setting.Value);
                    break;
            }
        });
    }

    // Review Focus 5: a path that escaped its root would write somewhere else entirely.
    [Fact]
    public void Every_key_path_is_relative_and_stays_inside_its_root()
    {
        Assert.All(Enum.GetValues<GpuVendor>().SelectMany(DriverSettings.For), setting =>
        {
            Assert.DoesNotContain("..", setting.KeyPath);
            Assert.DoesNotMatch(@"^[A-Za-z]:", setting.KeyPath);
            Assert.DoesNotContain("ControlSet", setting.KeyPath);   // choosing the control set is the service's job
        });
    }

    // XDocument.Parse throws on malformed XML, which is the point: a broken profile stops here
    // rather than reaching an installer.
    [Fact]
    public void The_NVIDIA_profile_is_well_formed_and_carries_settings()
    {
        var profile = XDocument.Parse(DriverSettings.NvidiaProfileXml);

        Assert.NotEmpty(profile.Descendants("ProfileSetting"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverSettingsTests"`
Expected: FAIL to compile — `DriverSettings` does not exist.

- [ ] **Step 3: Write `DriverSettings.cs`**

A static class holding the values, transcribed from AkariOS. `Root` says which of the three places a
value goes, and `KeyPath` is always **relative to that root**:

- **`Hkcu`** — below `HKEY_CURRENT_USER`.
- **`HklmServices`** — below `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services`.
- **`GraphicsClass`** — below each adapter subkey of
  `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}`.
  `KeyPath` names the part under each adapter (e.g. `UMD`); the service walks the adapter subkeys and
  applies the setting to each, which is what AkariOS's `foreach` does. There is no adapter number in
  the data, which is the point of this root.

The values:

- **NVIDIA**: `HklmServices` / `nvlddmkm\Parameters\Global\NVTweak` — `NvCplPhysxAuto` (Dword `0`),
  `NvDevToolsVisible` (Dword `1`), `RmProfilingAdminOnly` (Dword `0`), from `Graphics.ps1:286-290`.
  `NvidiaProfileXml` is **also** NVIDIA's: the `.nip` profile from `Graphics.ps1:300-352`, verbatim,
  applied by Nvidia Profile Inspector (a driver install writes `-clean`; the profile is separate).
- **AMD**: the `GraphicsClass` / `KMD_DeLagEnabled` and `UMD` values, and `power_v1`'s `abmlevel`
  (`Graphics.ps1:386-404`); the `Hkcu` / `Software\AMD\CN` values and its subkeys
  (`Graphics.ps1:382-412`).
- **Intel**: `GraphicsClass` / `3DKeys` (`Graphics.ps1:1383-1400`).

**Write `CurrentControlSet`, never `ControlSet001`** — AkariOS's `ControlSet001` is the control set
that happens to be current on the machine it was written on.

`Value` is the data as the type declares it: a Dword is plain decimal digits (AkariOS writes `/d 0`), a
String is the literal text, and a Binary is the hex bytes AkariOS passes (`/d "3000"` is two bytes, so
`"3000"`). Keeping the type on the setting is what stops the service guessing at the point of writing.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj --filter "FullyQualifiedName~DriverSettingsTests"`
Expected: PASS, 5 tests.

- [ ] **Step 5: Commit**

```bash
git add src/AkariDash.Core/Drivers/DriverSettings.cs src/AkariDash.Tests/DriverSettingsTests.cs
git commit -m "Drivers: the settings a configured install applies"
```

---

### Task 7: The driver service

**Files:**
- Create: `src/AkariDash.Core/Drivers/IDriverService.cs`
- Create: `src/AkariDash.Core/Drivers/DriverStepOutcome.cs`
- Create: `src/AkariDash.App/Services/WindowsDriverService.cs`
- Modify: `src/AkariDash.App/App.xaml.cs:192` (beside the other Action backends)

**Interfaces:**
- Consumes: everything from Tasks 1-6; `BuildInfo.IsDryRunOnly`; `IMachine.GpuVendors()`.
- Produces: `DriverStepOutcome` (enum: `Completed`, `CompletedNeedsRestart`, `InstalledWithoutSettings`, `HandedOffToInstaller`); `IDriverService` with `bool IsDryRun { get; }`, `Task<AvailableDriver?> LatestAsync(GpuVendor vendor, CancellationToken cancellationToken = default)`, `Task<string?> InstalledVersionAsync(GpuVendor vendor, CancellationToken cancellationToken = default)`, `Task<DriverStepOutcome> InstallAsync(DriverRow row, CancellationToken cancellationToken = default)`, `Task<DriverStepOutcome> DebloatAsync(DriverRow row, string installerPath, CancellationToken cancellationToken = default)`; `WindowsDriverService : IDriverService`.

- [ ] **Step 1: Write `DriverStepOutcome.cs` and `IDriverService.cs`**

```csharp
namespace AkariDash.Core.Drivers;

/// <summary>How a driver row ended. A failure is not one of these; the service throws instead.</summary>
public enum DriverStepOutcome
{
    /// <summary>The driver was installed, and its settings were applied when the row asked for them.</summary>
    Completed,

    /// <summary>Installed, but Windows wants a restart before it is usable.</summary>
    CompletedNeedsRestart,

    /// <summary>Installed, but the settings the row promised were not applied. Says so; does not pretend.</summary>
    InstalledWithoutSettings,

    /// <summary>The vendor's own installer was opened because it cannot be run unattended.</summary>
    HandedOffToInstaller,
}
```

`IDriverService`'s XML doc must say it is an Action backend (ADR-0004), and that `InstalledVersionAsync`
returns `null` for "could not ask", never for "nothing installed".

- [ ] **Step 2: Write `WindowsDriverService.cs`**

In `src/AkariDash.App/Services/`, in the style of `WindowsWingetAppService`: primary constructor,
`sealed class WindowsDriverService : IDriverService`, taking `IMachine machine` and
`ILogger<WindowsDriverService>`.

- `IsDryRun => BuildInfo.IsDryRunOnly;`
- **`LatestAsync`**: `GET DriverLookup.Endpoint(vendor)` with a **browser-like `User-Agent`** (AMD
  rejects the default; AkariOS spoofs Chrome at `Graphics.ps1:228-232`), one `HttpClient`, then
  `DriverLookup.Parse`. Any network or parse failure returns `null`; log it. Intel returns `null`
  without a request.
- **`InstalledVersionAsync`**: read the Display class key the way `WindowsMachine.GpuVendors()` does
  (`src/AkariDash.Core/Machine/WindowsMachine.cs:108-141`), mapping the adapter to its vendor by
  `MatchingDeviceId`, and return that subkey's `DriverVersion` value. Reading the machine directly
  here — rather than through `IMachine` — is the same choice `WindowsSystemCleanupService` makes; the
  *comparison* stays in Core, where it is tested.
- **`InstallAsync(row)`**: if `!DriverInstallCommand.IsUnattended(row.Vendor)`, open
  `DriverLookup.Endpoint(row.Vendor)` with `UseShellExecute = true` and return `HandedOffToInstaller`.
  Otherwise: `LatestAsync`; throw a readable `InvalidOperationException` when it is `null` (naming
  the vendor — an Action that cannot find the driver must say so, not install nothing); download to
  `%TEMP%\Akari-Dash\drivers\<vendor>\` with progress logged; run `DriverInstallCommand.Install`
  through the shared concurrent-stdout/stderr `ProcessStartInfo` helper (the one Task 4 of the
  Downloads plan built into `WindowsWingetAppService` — **extract it to a shared internal helper
  rather than copying it**); map exit codes the way `WingetInstallResult` does (0 → `Completed`,
  3010/1641 → `CompletedNeedsRestart`), then apply `DriverSettings.For(vendor)` — `Hkcu` and
  `HklmServices` resolve straight to their hive, and `GraphicsClass` walks the adapter subkeys under
  `...\Control\Class\{4d36e968-…}` and applies the value to each with that `KeyPath` — and return
  `InstalledWithoutSettings` if any of that fails. Write with `CurrentControlSet` in the path, never a
  hardcoded `ControlSet001`. For NVIDIA's `NvidiaProfileXml`, write it to the temp
  folder and run Nvidia Profile Inspector's `-silentImport -silent <file>` (`Graphics.ps1:351`) only if
  it is installed; when it is not, return `InstalledWithoutSettings` and say so.
- **`DebloatAsync(row, installerPath)`**: `DriverDebloat.Plan(row.Vendor, Path.GetFileName(installerPath))`;
  throw when it is `null` (the row should never have offered it). Create a fresh temp folder, extract
  the installer into it, delete the plan's entries, then run the extracted `setup.exe` with the same
  argument set. Falls back to `HandedOffToInstaller` for a vendor with no unattended install.
- **In every write path, if `IsDryRun`**: download nothing, run nothing, write nothing — log
  `DriverInstallCommand.Describe(...)` (and the settings that would be applied) and return
  `Completed`. Phase 1 must be safe to launch anywhere.

- [ ] **Step 3: Register the service**

In `src/AkariDash.App/App.xaml.cs`, beside the other Action backends (line 191-192):

```csharp
// Drivers (an Action backend, ADR-0004): finds, installs and configures the card's driver.
builder.Services.AddSingleton<IDriverService, WindowsDriverService>();
```

- [ ] **Step 4: Build and verify it compiles**

Run: `dotnet build AkariDash.slnx -c Phase1`
Expected: build succeeded, 0 errors.

- [ ] **Step 5: Run the whole test suite to verify nothing regressed**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj`
Expected: PASS, no failures.

- [ ] **Step 6: Commit**

```bash
git add src/AkariDash.Core/Drivers/IDriverService.cs src/AkariDash.Core/Drivers/DriverStepOutcome.cs src/AkariDash.App/Services/WindowsDriverService.cs src/AkariDash.App/App.xaml.cs
git commit -m "Drivers: finding, installing and configuring the card's driver"
```

---

### Task 8: The view model

**Files:**
- Create: `src/AkariDash.App/ViewModels/DriverRowViewModel.cs`
- Create: `src/AkariDash.App/ViewModels/DriverPipelineViewModel.cs`
- Modify: `src/AkariDash.App/App.xaml.cs:182` (page view model registrations)

**Interfaces:**
- Consumes: `IDriverService` (Task 7), `DriverRows` / `DriverRow` / `DriverStepKind` (Task 1), `DriverVersion` (Task 5), `DriverInbox` (Task 4), `IMachine`, `IDialogService`, `IInfoBarService`, `IStatusService`, `INavigationAware`, `ViewModelBase`.
- Produces: `DriverRowState` (enum: `Ready`, `Queued`, `Working`, `Done`, `NeedsRestart`, `HandedOff`, `Failed`); `DriverRowViewModel`; `DriverSectionViewModel` with `Vendor`, `Heading`, `DriverState`, `Rows`; `DriverPipelineViewModel` with `Sections`, `HasGraphicsCard`, `StatusText`, `IsBusy`, `DroppedInstallers`, `RunRowCommand`, `OpenInboxCommand`.

- [ ] **Step 1: Write `DriverRowViewModel.cs`**

In the style of `AppTileViewModel` (Downloads, Task 5): an `ObservableObject` wrapping one
`DriverRow`, exposing `Title`, `Description`, `RiskLabel` ("Caution" / "Advanced"), `State`
(`[ObservableProperty]`), a computed `StateText` ("Install", "Queued", "Working…", "Done", "Installed
— restart to finish", "Opened the installer", "Could not install"), and `IsRunnable` (true only when
`State` is `Ready` or `Failed`), `NotifyPropertyChangedFor`-linked from `State`. `RunCommand` is a
`[RelayCommand]` that does nothing; the page view model passes in the action, as
`AppTileViewModel` does.

- [ ] **Step 2: Write `DriverPipelineViewModel.cs`**

`sealed partial class DriverPipelineViewModel : ViewModelBase, INavigationAware`, constructor-injected
with `IDriverService drivers, IMachine machine, IDialogService dialogs, IInfoBarService infoBar,
IStatusService status, ILogger<DriverPipelineViewModel> logger`, and `Title = "Drivers"`.

- **Name it `DriverPipelineViewModel`.** `DriversViewModel` is already taken by the UWP Apps page's
  Drivers tab (`src/AkariDash.App/ViewModels/SystemCleanupViewModels.cs:214`).
- `OnNavigatedTo` kicks off `LoadAsync()` the way `UninstallerViewModel` does.
- **`LoadAsync`** builds one `DriverSectionViewModel` per vendor from `machine.GpuVendors()`, in a
  fixed order (NVIDIA, AMD, Intel). **Review Focus 3:** when the set is empty, `HasGraphicsCard` is
  false and `Sections` stays empty — the page then shows its "no graphics card found" state, and the
  rows are never built for a vendor that is not present. For each section, ask `LatestAsync` and
  `InstalledVersionAsync` and set its `DriverState` through `DriverVersion.State`, with a heading
  like *"NVIDIA GeForce RTX 4070 — 566.36 installed, 572.16 available"*, or *"— could not check the
  latest driver"* when `LatestAsync` returned `null`, or *"— could not read the installed version"*
  when that one did.
- **`DroppedInstallers`** is `DriverInbox.Installers(Directory.EnumerateFiles(DriverInbox.Folder))`
  (empty, not an error, when the folder does not exist). `OpenInboxCommand` opens `DriverInbox.Folder`
  in Explorer, creating it first — the same thing the Drivers tab does after an export
  (`SystemCleanupViewModels.cs:257-260`). The Debloat row's prompt is this list: **no file picker**
  (design Call 14).
- **`RunRowCommand(row)`** confirms first, in the spirit of `UninstallerViewModel`'s warning: the
  title names the row and the card (*"Install the latest NVIDIA driver?"*), and the message says what
  will happen and, plainly, that **it cannot be undone**. For a Debloat row, the confirm names the
  chosen file; when the inbox is empty, show the info bar saying where to put the installer and offer
  `OpenInboxCommand` instead of confirming.
  - **Dry run** (`drivers.IsDryRun`): queue nothing; show the command line the real run would use and
    leave the row's state alone.
  - Otherwise set `Queued` and start the pump if one is not running.
- **The pump** mirrors `DownloadsViewModel.RunQueueAsync`: one row at a time, each inside
  `using (_status.Begin($"Installing {row.Title}"))`, setting `Working` then the final state from the
  outcome. Catch per row — log it, set `Failed`, and **continue with the next row** — then report one
  summary in the info bar, carrying the service's own reason for each failure. `IsBusy` is true while
  the pump runs; `StatusText` names the row being run.

- [ ] **Step 3: Register the view model**

In `src/AkariDash.App/App.xaml.cs`, with the other page view models (line 182):

```csharp
builder.Services.AddTransient<DriverPipelineViewModel>();
```

- [ ] **Step 4: Build and verify it compiles**

Run: `dotnet build AkariDash.slnx -c Phase1`
Expected: build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/AkariDash.App/ViewModels/DriverRowViewModel.cs src/AkariDash.App/ViewModels/DriverPipelineViewModel.cs src/AkariDash.App/App.xaml.cs
git commit -m "Drivers: the page view model, its cards and the run queue"
```

---

### Task 9: The page

**Files:**
- Modify: `src/AkariDash.App/Views/DriversPage.xaml` (replace the placeholder)
- Modify: `src/AkariDash.App/Views/DriversPage.xaml.cs`
- Modify: `src/AkariDash.App/ShellPages.cs:15` (subtitle)

**Interfaces:**
- Consumes: `DriverPipelineViewModel`, `DriverRowViewModel`, `DriverSectionViewModel` (Task 8).
- Produces: nothing later tasks use.

- [ ] **Step 1: Write the page**

Replace `WorkInProgressView` in `DriversPage.xaml` with, top to bottom:

- A header row: the "Drivers" title, the status line (`StatusText`) and a `ProgressRing` bound to
  `IsBusy`, exactly as `UwpAppsPage.xaml:194-195` does.
- A `ScrollViewer` (or `ListView`) over `Sections`. Each section is a heading — the vendor's name and
  the card state line — followed by its rows.
- Each row: a title, its one-line description, its `RiskLabel` as a small caption, the `StateText`,
  a `ProgressRing` active while `State == Working`, and one button bound to `RunCommand`.
- **The no-card state** (`HasGraphicsCard` false): a short line saying no graphics card was found,
  instead of the sections. Bind it with the existing `BooleanToVisibilityConverter` and its inverse.
- A collapsible or plainly labelled area for the Debloat row's file list, bound to
  `DroppedInstallers`, with the `OpenInboxCommand` button and the path so a person can find the
  folder.

Keep the `x:Class`, the `views` namespace declaration, and set `DataContext` in the code-behind.

- [ ] **Step 2: Write the code-behind**

```csharp
public sealed partial class DriversPage : Page
{
    public DriverPipelineViewModel ViewModel { get; }

    public DriversPage(DriverPipelineViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 3: Give the nav item a real subtitle**

In `src/AkariDash.App/ShellPages.cs`, replace `Subtitle: "Work in progress"` on the Drivers line
(line 15) with `Subtitle: "Install and configure the graphics driver"`. **Do not mention the clean
yet** — there is no clean row until the second plan, and the subtitle must say what the page actually
does. That plan extends this line.

- [ ] **Step 4: Build and launch the page**

Run: `dotnet build AkariDash.slnx -c Phase1`
Expected: build succeeded, 0 errors.

Then run the Phase 1 build and open **Drivers** in the sidebar. Expected: one section per card the
machine actually has, each naming the card and what is installed; the rows act as though pressed but
nothing is downloaded and the info bar shows the `setup.exe -s -noreboot …` line it would have run.
On a machine with no graphics card the page says so. No crash on navigation. **Note:** in Phase 1
`LatestAsync` still makes its network call (reads are live), so this step needs a network connection.

- [ ] **Step 5: Run the whole test suite to verify nothing regressed**

Run: `dotnet test src/AkariDash.Tests/AkariDash.Tests.csproj`
Expected: PASS, no failures.

- [ ] **Step 6: Commit**

```bash
git add src/AkariDash.App/Views/DriversPage.xaml src/AkariDash.App/Views/DriversPage.xaml.cs src/AkariDash.App/ShellPages.cs
git commit -m "Drivers: the page itself, a section per card with its rows"
```

---

### Task 10: The VM build and its check

**Files:**
- Create: `vm-build/VM-CHECK-DRIVERS.md`

**Interfaces:**
- Consumes: everything above.
- Produces: the built app in `vm-build/AkariDash-Debug` and the steps to test it.

- [ ] **Step 1: Publish the Debug build for the VM**

Run: `dotnet publish src/AkariDash.App/AkariDash.App.csproj -c Debug -o vm-build/AkariDash-Debug`
Expected: the folder holds `Akari Dash.exe` beside the files it already had, refreshed.

- [ ] **Step 2: Write `vm-build/VM-CHECK-DRIVERS.md`**

In the style of `vm-build/VM-CHECK-UWP-APPS.md`: a title, the reminder that this is **VM only** and
the path to the exe (`vm-build\AkariDash-Debug\Akari Dash.exe`), then numbered steps that each say
what to do and what to expect, each with an independent way to confirm the result **inside the VM**.
Cover at least:

1. The nav item is **Drivers** with the new subtitle, and the page shows one section per graphics
   card the VM actually has. Compare against
   `Get-PnpDevice -Class Display | Format-Table FriendlyName` in an elevated PowerShell.
2. **The version read-back.** Note the installed version on the card's heading and confirm it against
   the vendor's own tool (or Device Manager → the adapter → Driver tab). It must match.
3. **Install**, on a card where an update exists: the confirm names the card and says it cannot be
   undone. Cancel: nothing happens. Confirm: the status line names the row, it ends as installed, and
   the heading's version changes to the new one. Confirm independently with the vendor's tool and
   with Device Manager.
4. **Install and configure**: confirm the settings were applied, using the vendor's own control panel
   (`GPU-Z` or `nvidia-smi -q` for the NVIDIA profile; `reg query HKCU\Software\AMD\CN` for AMD).
5. **AMD and Intel hand-off**: the row opens the vendor's page/installer rather than claiming a silent
   install, and says so afterwards.
6. **Debloat**: drop a downloaded installer into `Documents\Akari-Dash\Downloaded drivers`. The row
   lists it; with the folder empty, the row points at the folder and opens it. Run it and confirm the
   driver installs and the stripped components are absent from the extracted folder.
7. **A file that is not that vendor's installer** (rename a text file to `.exe` and drop it in): the
   row refuses it and nothing is deleted.
8. **No card**: in a VM with no PCI graphics card the page says so, and the rows never appear. Confirm
   `IMachine.GpuVendors()` is empty there by checking `Get-PnpDevice -Class Display`.
9. **A failure**: take the VM offline and install: the row reads as a failure with a readable reason,
   and **the rest of the page still works**. Bring the network back and the row is runnable again.
10. **Phase 1 is inert**: run the `Phase1` build and confirm the same clicks download nothing, run
    nothing and write nothing, while reporting the command lines.
11. **Both endpoints, live**: from the VM, run the two `Invoke-WebRequest` commands from Task 2 Step 5
    and confirm each still returns what the parser expects. Record the driver versions seen.

**This plan's steps do not touch the DDU clean or any safe-mode reboot.** Nothing here changes the
boot configuration.

- [ ] **Step 3: Commit**

```bash
git add vm-build/VM-CHECK-DRIVERS.md
git commit -m "Drivers: what to check in the VM"
```

---

## Notes for the executor

- **The DDU clean and the guided "Reset and reinstall" card are deliberately not in this plan.** They
  are the design's second step, they depend on these rows, and they get their own plan. Do not add a
  DDU, safe-mode or reboot step to any task above.
- `vm-build/` is in the repository and is committed, but `AkariDash-Debug/`'s binary output is build
  output; add only the Markdown to the commit unless the repo already tracks those binaries (check
  `git status` before `git add`).
- **The `psid`/`pfid` in NVIDIA's endpoint identify a product family, and AkariOS's pair may not be
  the right one for the card in the VM.** Task 2 Step 5 is where that is checked. If it cannot be
  resolved for a card, the honest fallback is the NVIDIA download page and a `HandedOffToInstaller`
  row — not a request that quietly returns another card's driver.
- **AMD and Intel cannot be installed unattended**, which is why their rows hand off. Confirm that
  this is still true when the VM check runs; if a silent switch is found, the change is in
  `DriverInstallCommand` alone, and the test in Task 3 already pins the shape.
- **The settings in Task 6 overlap the GPUs page (#33) by design.** They live here once; #33 consumes
  them when it becomes toggles. If you change a value, that page must change with it.
