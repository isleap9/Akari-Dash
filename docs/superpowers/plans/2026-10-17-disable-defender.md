# Disable Defender Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port AkariOS-Ultimate's Advanced "Defender" tweak (`adv-defender`) whole, as a two-Option DeclaredTweak "Disable Defender" on the Security page that rides the existing safe-mode companion, and ship a Debug build to `vm-build\AkariDash-Debug` for VM-only verification.

**Architecture:** New catalog data in `TweakCatalog.Security.cs` sharing the existing rows' locations; the safe-mode pass exchange becomes a queue (list) in the one JSON file so two safe-mode Tweaks can be staged at once; the companion (`AkariDash.DefenderReset.exe`) runs every pending pass in one restart, adds the DISM-feature and `smartscreen.exe`-move best-effort pieces for this Tweak, and keeps phishing/ExploitGuard ride-alongs shared by both safe-mode Tweaks.

**Tech Stack:** C# / .NET (pinned by `global.json`, Windows App SDK app, x64), xUnit. No new package references.

**Spec:** `docs\superpowers\specs\2026-10-17-disable-defender-design.md` — the target-by-target tables in "The Tweak, target by target" are the source of every key, value and AbsentMeans; the source values come from `C:\Users\isleap\Documents\GitHub\AkariOS-Ultimate\Tweaks\Advanced.ps1` lines 3–435 (MIT), cited in comments as the Defender optimize code does.

## Global Constraints

- **Never run the app, the companion, or any tweak on this machine.** The author's PC is not a test bench; VM verification is the user's. Only `dotnet build` / `dotnet test` / `dotnet publish` run here.
- Tests touch only in-memory machines and temp files (`InMemoryMachine`, `DryRunMachine`) — never the real registry, services, tasks or boot store.
- Target counts the tests must pin: **56 targets total, 42 pass targets, 14 own-session.**
- Copy voice: descriptions are single flowing sentences in the repo's prose; code comments name the source file and lines like the Defender optimize comments do.
- Source values are final settled values: AkariOS's enable list writes `PUAProtection` 2 then 1 — On writes 1.
- Commits follow the repo's style: short `Category: what it does` subjects (see `git log`).

## Review Focus

- **Two staged passes at once** (`security.defender-optimize` pending, then `Disable Defender` applied): the user expects *both* to run in the one safe-mode restart, not the second overwriting the first. → Pinned by `SafeModePassTests` multi-entry round-trip (Task 1) and the VM-CHECK "both Tweaks staged" step (Task 6).
- **Same Tweak re-staged before the restart** (Off applied, then On applied): the user expects only their last choice to run. → `SafeModePassTests.Re_appending_a_tweaks_pass_replaces_it` (Task 1).
- **Windows 11 without the DISM feature** (it does not exist there, so `Dism /Disable-Feature` fails every time): the user expects the pass to still count as applied. → VM-CHECK step asserting Stage=Applied and the log line, plus the Task 5 code review note; not unit-testable (companion exe).
- **An edition without `Sense` or `webthreatdefsvc`** (Home): the user expects the row to read On/Off, never Custom because of a service it never had. → `DefenderOffTests.Absent_services_read_as_windows_default` (Task 4).
- **Tamper protection rewriting the four Defender tasks back on** (documented on `security.defender-tasks`): the user expects the row to read On again honestly rather than lie. → Shared with the existing row; Task 4 pins Off writes them Off via the shared targets.

---

### Task 1: The safe-mode pass exchange becomes a queue

**Files:**
- Modify: `src\AkariDash.Core\Machine\SafeModePass.cs`
- Create: `src\AkariDash.Tests\SafeModePassTests.cs`

**Interfaces:**
- Consumes: the existing `SafeModePass` record, `SerializerOptions`, `Read`, `Write`, `Remove` (unchanged signatures).
- Produces (Task 2 and the companion use exactly these):
  - `static IReadOnlyList<SafeModePass> ReadList(string path)` — `[]` when the file is absent; a legacy single-record file reads as one entry.
  - `static void WriteAll(string path, IReadOnlyList<SafeModePass> passes)` — serializes as a JSON array; empty list deletes the file.
  - `static void Append(string path, SafeModePass pass)` — reads what is there, drops every entry whose `TweakId` equals `pass.TweakId`, appends, writes. A re-staged Tweak therefore runs last: the queue reflects the order the user last asked for things, which is what the staging writes of two Tweaks sharing locations must follow (the user's latest choice for that Tweak wins; a re-staged pass renews its intent at the newest point in time).
  - `static void RemoveTweak(string path, string tweakId)` — rewrites without that TweakId's entries; file deleted when nothing is left.

- [ ] **Step 1: Write the failing tests**

```csharp
using AkariDash.Core.Machine;
using Xunit;

namespace AkariDash.Tests;

/// <summary>The pass exchange is a queue now: two safe-mode Tweaks can be pending at once, and the
/// same Tweak re-staged before the restart replaces its own entry.</summary>
public class SafeModePassTests
{
    private static string SomePath() => Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.json");

    [Fact]
    public void Passes_round_trip_as_a_list()
    {
        var path = SomePath();
        SafeModePass.WriteAll(path, [
            new SafeModePass("security.defender-optimize", "optimize", Undo: false),
            new SafeModePass("security.disable-defender", "off", Undo: false),
        ]);

        Assert.Equal(
            ["security.defender-optimize", "security.disable-defender"],
            SafeModePass.ReadList(path).Select(pass => pass.TweakId));

        File.Delete(path);
    }

    // Files written by the previous single-pass app must still read: one record is a one-item list.
    [Fact]
    public void A_legacy_single_record_file_reads_as_one_entry()
    {
        var path = SomePath();
        new SafeModePass("security.defender-optimize", "optimize", Undo: false).Write(path);

        var passes = SafeModePass.ReadList(path);

        var pass = Assert.Single(passes);
        Assert.Equal(new SafeModePass("security.defender-optimize", "optimize", Undo: false), pass);

        File.Delete(path);
    }

    [Fact]
    public void Appending_replaces_the_same_tweaks_entry_in_place_and_keeps_the_others()
    {
        var path = SomePath();
        SafeModePass.Append(path, new SafeModePass("a", "off", Undo: false));
        SafeModePass.Append(path, new SafeModePass("b", "off", Undo: false));
        SafeModePass.Append(path, new SafeModePass("a", "on", Undo: false));

        var passes = SafeModePass.ReadList(path);

        Assert.Equal(2, passes.Count);
        Assert.Equal("b", passes[0].TweakId);
        Assert.Equal(("a", "on"), (passes[1].TweakId, passes[1].OptionId));

        File.Delete(path);
    }

    [Fact]
    public void RemoveTweak_takes_only_its_own_entries_and_deletes_an_empty_file()
    {
        var path = SomePath();
        SafeModePass.Append(path, new SafeModePass("a", "off", Undo: false));
        SafeModePass.Append(path, new SafeModePass("b", "off", Undo: false));

        SafeModePass.RemoveTweak(path, "a");
        Assert.Equal(["b"], SafeModePass.ReadList(path).Select(pass => pass.TweakId));

        SafeModePass.RemoveTweak(path, "a"); // no entry for it: no harm
        SafeModePass.RemoveTweak(path, "b");
        Assert.Empty(SafeModePass.ReadList(path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void A_missing_file_reads_as_nothing()
    {
        Assert.Empty(SafeModePass.ReadList(SomePath()));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test src\AkariDash.Tests --filter SafeModePassTests`
Expected: FAIL — `ReadList`/`WriteAll`/`Append`/`RemoveTweak` do not exist.

- [ ] **Step 3: Implement the four static methods in `SafeModePass.cs`**

Body shape: `Append` and `RemoveTweak` read the list (`ReadList`), transform it, and write back (`WriteAll`); `WriteAll` serializes the list with the existing `SerializerOptions` and deletes the file when the list is empty; `ReadList` deserializes either a list or a single record — distinguish by the file text's first non-whitespace character (`'['`). The temp-file move pattern from `Write` can be reused for the rewrites so a concurrent reader never sees half a file.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test src\AkariDash.Tests --filter SafeModePassTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/AkariDash.Core/Machine/SafeModePass.cs src/AkariDash.Tests/SafeModePassTests.cs
git commit -m "Safe mode: the pass exchange holds a queue of pending passes"
```

---

### Task 2: The app stages into the queue; the companion runs every pending pass

**Files:**
- Modify: `src\AkariDash.Core\Machine\WindowsMachine.cs:398-463` (`StageSafeModePass`, `RemoveRunOnceValue`)
- Modify: `src\AkariDash.DefenderReset\Program.cs`

**Interfaces:**
- Consumes: Task 1's `Append`, `RemoveTweak`, `ReadList`, `WriteAll`.
- Produces: `WindowsMachine.StageSafeModePass(SafeModePass)` keeps its exact signature — the engine (`TweakEngine.cs:271,320`) and every test pass unchanged. `Program.cs` likewise keeps its argument surface (`--pass`, `--elevated`).

- [ ] **Step 1: Switch the staging to the queue in `WindowsMachine.StageSafeModePass`**

`pass.Write(SafeModePass.DefaultPath)` becomes `SafeModePass.Append(SafeModePass.DefaultPath, pass)`. The two rollback `catch` blocks call `SafeModePass.Remove(SafeModePass.DefaultPath)`; both become `SafeModePass.RemoveTweak(SafeModePass.DefaultPath, pass.TweakId)` — roll back only what this staging added, never another Tweak's pending pass. `Remove` then has no callers: delete it and fix any compiler complaint.

WindowsMachine is real-machine code the repo verifies in the VM (see its class comment); its checkable deliverable here is the build plus the unchanged engine tests.

- [ ] **Step 2: Companion reads the list and works every `Scheduled` entry**

In `Program.cs`:
- The orchestrator's `Read()` becomes `SafeModePass.ReadList(passPath)`.
- `Work()` loops over the entries whose Stage is `Scheduled` in order; for each it writes that pass's values exactly as today (the `Values(pass)` lookup is already generic by `TweakId`), records the outcome into the **in-memory list** it was called with, and after the loop saves the whole list once with `SafeModePass.WriteAll`. The per-pass Outcome rules are untouched: `failures.Count == 0 ? Applied : Failed`, and the Original-Values `Clear` on a clean undo is per pass (per `TweakId`).
- The orchestrator's staged/failed checks after `RunTrusted()` look at whether any entry is still `Stage.Scheduled` rather than one record.
- The RunTrusted started-but-unfinished wait loop waits while any entry is still `Scheduled`.
- Window title becomes `"Akari Dash - safe mode pass"` and each log line names the pass's Tweak and Option (`Applying security.disable-defender off.` / `Putting Defender optimize's settings back.`), so the person watching the restart can tell which of the two is running.

- [ ] **Step 3: Build and run the existing safe-mode tests to verify nothing moved**

Run: `dotnet test src\AkariDash.Tests --filter "DefenderOptimizeTests|SafeModePassTests"`
Expected: PASS — the engine still stages exactly one pass per apply (`Assert.Single(machine.SafeModePasses)` holds; `InMemoryMachine.StageSafeModePass` is untouched).

Run: `dotnet build`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AkariDash.Core/Machine/WindowsMachine.cs src/AkariDash.DefenderReset/Program.cs src/AkariDash.Core/Machine/SafeModePass.cs
git commit -m "Safe mode: staging appends to the queue and the companion works every pending pass"
```

---

### Task 3: The Defender ride-alongs exist once, shared by both safe-mode Tweaks

Both safe-mode Tweaks ride the same two extras (the unreadable `WTDS\Components` phishing key's five values and the Exploit Guard MDM policy refresh task) with identical Off/Optimize and On/Undo values, so the companion needs one call, not a dispatch.

**Files:**
- Create: `src\AkariDash.Core\Tweaks\DefenderRideAlong.cs`
- Modify: `src\AkariDash.Core\Tweaks\DefenderOptimize.cs:55-62` (`Extra` delegates)
- Modify: `src\AkariDash.DefenderReset\Program.cs:108` (call the shared method)

**Interfaces:**
- Produces: `public static class DefenderRideAlong` with `public static IReadOnlyList<(MachineLocation Location, MachineValue? Value)> Defender(bool disable)` — the exact list `DefenderOptimize.Extra` produces today (phishing five `RegistryValue.DWord(disable ? 0 : 1)`, ExploitGuard task `disable ? TaskEnabledValue.Off : TaskEnabledValue.On`).
- `DefenderOptimize.Extra(bool optimize)` keeps its public signature and body becomes `DefenderRideAlong.Defender(optimize)` — `DefenderOptimizeTests` (lines 166–195) must pass unchanged.

- [ ] **Step 1: Move the list into `DefenderRideAlong.Defender(bool disable)`** — the `PhishingKey`/`PhishingValues`/`ExploitGuardTask` constants move with it; `DefenderOptimize` delegates.

- [ ] **Step 2: Point `Program.cs:108` at `DefenderRideAlong.Defender(!pass.Undo)`** (replacing `DefenderOptimize.Extra(optimize: !pass.Undo)`).

- [ ] **Step 3: Run the tests to verify the ride-along pins still hold**

Run: `dotnet test src\AkariDash.Tests --filter "DefenderOptimizeTests|SafeModePassTests"`
Expected: PASS — `Phishing_protection_and_the_exploit_guard_task_ride_along_in_the_pass` proves the values did not move.

- [ ] **Step 4: Commit**

```bash
git add src/AkariDash.Core/Tweaks/DefenderRideAlong.cs src/AkariDash.Core/Tweaks/DefenderOptimize.cs src/AkariDash.DefenderReset/Program.cs
git commit -m "Defender: the ride-along extras are shared by every Defender safe-mode pass"
```

---

### Task 4: The Tweak — catalog data and its tests

**Files:**
- Create: `src\AkariDash.Core\Tweaks\DisableDefender.cs` (the companion's `TweakId` constant, beside `DefenderOptimize.cs`)
- Modify: `src\AkariDash.Core\Tweaks\TweakCatalog.Security.cs` (add `DisableDefender()` to `SecurityTweaks()`; extend the file's top comment with the new row)
- Create: `src\AkariDash.Tests\DefenderOffTests.cs`
- Modify: `src\AkariDash.Tests\SecurityCatalogTests.cs` (the ownership test)

**Interfaces:**
- Produces: `DeclaredTweak` id `"security.disable-defender"`, title `"Disable Defender"`, `Category.Security`, Group `"Microsoft Defender"`, no `Recommended`, `Activation.InSafeMode`; Options `"off"` ("Off") and `"on"` ("On"). 56 targets: 42 pass targets, 14 own-session. And `public static class DisableDefender { public const string TweakId = "security.disable-defender"; }`.
- Consumes: `TweakTarget`, `RegistryLocation`, `BootSettingLocation`, `ScheduledTaskLocation`, `RegistryValue`, `BootSettingValue`, `TaskEnabledValue` exactly as `DefenderOptimizeTweak()` uses them.

- [ ] **Step 1: Write the failing tests in `DefenderOffTests.cs`**

```csharp
using AkariDash.Core.Machine;
using AkariDash.Core.Tweaks;
using Microsoft.Win32;
using Xunit;

namespace AkariDash.Tests;

/// <summary>AkariOS-Ultimate's Advanced 'Defender' Tweak (Tweaks/Advanced.ps1:3-435): one row that
/// turns Defender off and on, written in safe mode like Defender optimize.</summary>
public class DefenderOffTests
{
    private static DeclaredTweak Tweak => TweakCatalog.All.Single(tweak => tweak.Id == "security.disable-defender");
    private static TweakOption Option(string id) => Tweak.Options.Single(option => option.Id == id);

    private static MachineValue? At(string key, string name) => At(new RegistryLocation(RegistryHive.LocalMachine, key, name));
    private static MachineValue? At(MachineLocation location) => Option(CurrentOption).Values[Tweak.Targets.Single(target => target.Location == location)];
    private static string CurrentOption = "off";

    private static RegistryValue Binary(string hex) => new(RegistryValueKind.Binary, Convert.FromHexString(hex));
```

Then these tests (the assertions are value-for-value from the spec's tables — write every line):

1. `The_row_is_the_disable_defender_row_of_the_security_page` — title, group `"Microsoft Defender"`, `Activation.InSafeMode`, `Recommended` null, Options exactly `["off", "on"]` labelled exactly `["Off", "On"]`.
2. `Off_writes_what_akarios_advanced_defender_writes` — asserts, by location: real-time values 1/1, Spynet 0/0, TamperProtection 4, CFA 0, PUA 0, TrustMode 0, SmartLockerMode 0, `SMARTLOCKER\START_PENDING` 0, `SMARTLOCKER\ENABLED` `Binary("0000000000000000")`, `VerifiedAndReputablePolicyState` 0, the four Security-Center notification values 1, `Epoch` 1231, the three firewall profiles 1, `SmartScreenEnabled` `"Off"`, `MitigationOptions` `Binary("222222000001000000000000000000000000000000000000")` (Advanced.ps1:104's mask — it is *not* the optimize Tweak's mask), memory integrity `0`/`null`/`null`, `EnableVirtualizationBasedSecurity` null, the three boot settings null, `RunAsPPL` 0, `VulnerableDriverBlocklistEnable` 0, account protection three values 1, Edge two values 0, AppHost 0, the four tasks `TaskEnabledValue.Off`, and the eleven service/driver `Start` values all `RegistryValue.DWord(4)`.
3. `The_services_and_drivers_are_registry_start_values_at_their_windows_default_when_absent` — every one of the eleven lives at `HKLM\SYSTEM\CurrentControlSet\Services\{name}` value `"Start"`; its `AbsentMeans` is the On value from the spec (WdBoot/WdFilter 0 Boot, WinDefend/MDCoreSvc/wscsvc/webthreatdefusersvc/SecurityHealthService 2, the rest 3); none is a `ServiceLocation` and none is `WrittenInOwnSession`.
4. `On_writes_what_akarios_enable_list_writes` — set `CurrentOption = "on"` at the top and assert: real-time 0/0, Spynet 2/1, TamperProtection 5, CFA 1, PUA 1 (its settled value, not the 2 it is first written as), TrustMode 1, SmartLockerMode 1, `START_PENDING` 4, `ENABLED` `Binary("0400000000000000")`, PolicyState 1, the four notification values 0, `Epoch` 1228, firewall 0s, `SmartScreenEnabled` `"Warn"`, `MitigationOptions` null (Windows default — my call, flagged in the spec), memory integrity 1/2/null, `EnableVirtualizationBasedSecurity` 1 (my call — AkariOS's enable leaves VBS off; the spec restores it), hypervisorlaunchtype `Auto`, the other two boot settings null, `RunAsPPL` 2, blocklist 1, account protection 0s, Edge/AppHost 1s, tasks `TaskEnabledValue.On`, and the eleven `Start`s per the spec's On column (3/2/2/2/3/2/3/2 and 0/0/3). Reset `CurrentOption` back to `"off"` at the end.
5. `Off_and_on_own_the_tray_and_the_menu_entries_in_the_own_session` — the three EPP default-value targets (`SOFTWARE\Classes\Directory\shellex\ContextMenuHandlers\EPP`, `...\Drive\...`, `...\*\...`, value name `""`) are `WrittenInOwnSession`, Off writes null and On writes `RegistryValue.String("{09A47860-11B0-4DA5-AFA5-26D86198A780}")` with that string as `AbsentMeans`; `Run\SecurityHealth` is `WrittenInOwnSession`, Off null, On `new RegistryValue(RegistryValueKind.ExpandString, "%windir%\\system32\\SecurityHealthSystray.exe")` with the same as `AbsentMeans`; and the four Defender tasks plus the three HKCU account-protection and three HKCU Edge/AppHost values are the rest of the own-session set.
6. `The_pass_targets_are_the_rest` — `DefenderOptimize.PassTargets(Tweak)` (the generic own-session filter) answers exactly 42 of the 56, contains no `ScheduledTaskLocation`, no `RegistryHive.CurrentUser`, no EPP or `Run` key.
7. `Applying_off_stages_a_pass_and_writes_nothing_safe_mode` — mirror `DefenderOptimizeTests.Applying_...`: `InMemoryMachine` seeded with the four tasks On, apply Off, `machine.SafeModePasses` holds exactly `new SafeModePass("security.disable-defender", "off", Undo: false)`, nothing was written, `IsApplied` true.
8. `Applying_off_writes_the_own_session_values_now` — after apply: Edge `SmartScreenEnabled` reads `DWord(0)`, EPP default value reads null, `Run\SecurityHealth` reads null, a task reads `TaskEnabledValue.Off`; still exactly one pass staged.
9. `Undoing_stages_a_pass_and_keeps_the_original_values` — mirror the optimize tests verbatim, ids swapped.
10. `A_staged_pass_is_pending_until_the_restart` and `A_dry_run_previews_without_staging` — mirror the optimize tests. For the dry run: on a machine seeded with all of On's non-null values, `Preview(Tweak, off)` lists every Off value that differs from On's — the 11 `Start` changes, the 4 EPP/Run deletes, and the rule the optimize test states (null-to-null is nothing to show).

11. `An_untouched_pc_reads_as_on_rather_than_off` — seed all non-null On values into a fresh `InMemoryMachine` (as `SecurityCatalogTests.An_untouched_pc_reads_as_windows_default_rather_than_optimize` does), `ReadLiveState` answers `"on"`.
12. `Absent_services_read_as_windows_default` — the largest edition hole: `ReadLiveState` on a machine seeded with all of On's *registry* values (no `Start` values present at all) answers `"on"`, because every `Start` target's `AbsentMeans` is its On value.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test src\AkariDash.Tests --filter DefenderOffTests`
Expected: FAIL — no Tweak `security.disable-defender`.

- [ ] **Step 3: Update the ownership test in `SecurityCatalogTests.cs`**

`Only_defender_optimize_owns_defenders_own_keys_and_smart_app_control` becomes `Only_the_safe_mode_rows_own_defenders_own_keys_and_smart_app_control`: the exclusion set is `["security.defender-optimize", "security.disable-defender"]`. Everything else in that file must keep passing.

- [ ] **Step 4: Implement `DisableDefender.cs` and `TweakCatalog.Security.cs`**

- `DisableDefender.cs`: the `TweakId` constant and a doc comment naming its source (`Tweaks/Advanced.ps1:3-435`).
- `DisableDefender()` as a new private static method beside `DefenderOptimizeTweak()`, same style: `const string` keys, local `Target(...)`/`Start(...)`/`Epp(...)` factory funcs, `TweakTarget[]` section arrays in the spec's order, and `Off()` / `On()` local funcs building the two `TweakOption` dictionaries from those arrays. The description is the spec's "Description (draft)" section, verbatim. Add the row to `SecurityTweaks()` and one sentence to the file's top comment pointing at the new method.

- [ ] **Step 5: Run to verify they pass** — `dotnet test src\AkariDash.Tests --filter "DefenderOffTests|SecurityCatalogTests|TweakCatalogTests"` — Expected: PASS

- [ ] **Step 6: Run the whole suite** — `dotnet test src\AkariDash.Tests` — Expected: PASS (a second safe-mode row must break nothing else: `HomeViewModel`-pending and search treat it generically).

- [ ] **Step 7: Commit**

```bash
git add src/AkariDash.Core/Tweaks/DisableDefender.cs src/AkariDash.Core/Tweaks/TweakCatalog.Security.cs src/AkariDash.Tests/DefenderOffTests.cs src/AkariDash.Tests/SecurityCatalogTests.cs
git commit -m "Security: Disable Defender — the full Advanced akarios off/on switch"
```

---

### Task 5: The companion's best-effort pieces: DISM and the smartscreen move

Companion-only (`WindowsMachine` is verified in a VM by design, and the companion exe is not unit-tested in this repo) — the pins are the build, the built output, and the VM-CHECK steps.

**Files:**
- Modify: `src\AkariDash.DefenderReset\Program.cs`

**Interfaces:**
- Consumes: Task 4's `DisableDefender.TweakId`.
- Semantics from the spec, settled after the builder check: **best-effort channel** — every outcome is `Say`-logged; nothing here ever fails the pass or blocks an Undo's Original-Values clearing. They run unconditionally for this Tweak's pass (apply *and* undo), as AkariOS's script runs DISM regardless.

- [ ] **Step 1: Implement, guarded by `pass.TweakId == DisableDefender.TweakId`**

- Apply (not undo, both as the TrustedInstaller worker): run `Dism.exe /Online /NoRestart /Disable-Feature /FeatureName:Windows-Defender-Default-Definitions` from `Environment.SystemDirectory`, log exit code and output; then `File.Move(@"%SystemRoot%\System32\smartscreen.exe", @"%SystemRoot%\smartscreen.exe", overwrite: true)` — a missing source is a log line ("already moved"), not an error.
- Undo: move `C:\Windows\smartscreen.exe` back to `System32` (overwrite); `C:\Windows\smartscreen.exe` missing → log line, not an error. **DISM is never re-enabled** (Windows cannot); log that the feature stays disabled.
- These calls never add to `failures`; the pass's Stage stays whatever the target writes decided.

- [ ] **Step 2: Build and run the full suite**

Run: `dotnet build` then `dotnet test src\AkariDash.Tests`
Expected: all PASS.

- [ ] **Step 3: Commit**

```bash
git add src/AkariDash.DefenderReset/Program.cs
git commit -m "Defender off: the companion's best-effort DISM disable and smartscreen move"
```

---

### Task 6: Ship it to the VM — checklist and the Debug build

**Files:**
- Create: `vm-build\VM-CHECK-DISABLE-DEFENDER.md`
- Create: build output under `vm-build\AkariDash-Debug\` (not committed — it is the user's test bed, like the existing checklists)

**Interfaces:**
- Consumes: everything above. Produces the deliverable the user tests in a VM.

- [ ] **Step 1: Write `vm-build\VM-CHECK-DISABLE-DEFENDER.md`**, named and shaped like the other `VM-CHECK-*.md` files. It must cover, in order:
  1. Restore-point prompt, then apply Off on the Security page → the PC restarts into safe mode and back, once.
  2. After the restart: `sc qc WinDefend` shows Start 4 (all eleven if handy); Windows Security shows no antivirus and no tray icon; Defender's entries are gone from file right-click menus; `C:\Windows\smartscreen.exe` present and `C:\Windows\System32\smartscreen.exe` absent; `Get-ScheduledTask` shows the four Defender tasks Disabled; the DisM feature state (`Get-WindowsOptionalFeature -Online -FeatureName Windows-Defender-Default-Definitions` — on a Windows 10 VM only; the Win11 VM reports the feature missing and the pass must still read applied).
  3. On Windows 11 the DISM failure must be a log line under `C:\ProgramData\Akari-Dash\logs\defender-reset.log`, never a failed pass.
  4. Stage `security.defender-optimize` too, so two passes are pending; apply Disable Defender; restart → both Tweaks' values on the machine (optimize's Optimize plus Defender fully off).
  5. Apply On (or press Undo) → Defender back: services at their defaults, tray and context menus back, smartscreen back in System32, VBS and hypervisor boot settings restored; the DISM feature stays disabled and the row's description says so.
  6. The row reads Off while off and On once enabled, and drift is described honestly if Windows turns the tasks back on.

- [ ] **Step 2: Publish the Debug build the user can copy into the VM**

Run (from the repo root):

```powershell
dotnet publish src\AkariDash.App\AkariDash.App.csproj -c Debug -p:Platform=x64 -o vm-build\AkariDash-Debug
```

Then verify these exist under `vm-build\AkariDash-Debug\`: `Akari Dash.exe`, `Akari Dash.pri`, `Views\*.xbf`, and `AkariDash.DefenderReset.exe` beside them (the companion must ship with the app — without it nothing gets staged). Expected: all present.

- [ ] **Step 3: Final full test run** — `dotnet test src\AkariDash.Tests` — Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add vm-build/VM-CHECK-DISABLE-DEFENDER.md docs/superpowers/specs/2026-10-17-disable-defender-design.md
git commit -m "Defender off: the VM checklist and the design the row was built from"
```

(The spec is committed here if it never was; confirm with `git status` first.)
