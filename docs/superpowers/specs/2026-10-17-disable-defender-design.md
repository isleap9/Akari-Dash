# Disable Defender — design

_2026-10-17. Porting AkariOS-Ultimate's Advanced "Defender" Tweak (`adv-defender`, `Tweaks/Advanced.ps1`, MIT) into Akari-Dash as one Tweak that turns Windows Defender off and on._

## Intent and the why

Akari-Dash has AkariOS-Ultimate's "Defender optimize" (`security.defender-optimize`), which switches off Defender's protections and accoutrements but leaves the antivirus itself running. The Advanced tweak goes further: Defender effectively gone from the PC — real-time and cloud protection off, its eight services and three drivers disabled, its tray icon and right-click menu entries removed. The ask is that whole Tweak as one row the user can apply and undo. The user chose a **faithful full port**, including the two pieces that do not come back cleanly, over a softened version.

## Goals

- One new DeclaredTweak, `security.disable-defender`, titled **"Disable Defender"**, in **Category.Security**, Group **"Microsoft Defender"** (there is no Advanced page in Akari-Dash; this is where a user looks for Defender — the user chose this).
- Two Options: `off` ("Off") and `on` ("On"). Off is AkariOS's disable list; On is its enable list, value for value (final values — its enable list writes `PUAProtection` 2 and then 1 again; On writes 1).
- No Recommended Option: like the rest of the Security page, this trades protection for performance and is the user's call, never "apply all recommended"'s.
- Apply and Undo ride the **existing safe-mode pass machinery** — the `AkariDash.DefenderReset` companion, TrustedInstaller in safe boot, one restart, the flow `security.defender-optimize` already uses. No new restart mechanism.
- Live State reads back honestly; Undo puts Original Values back; sharing targets with the other Security rows stays per-location, as CONTEXT.md already defines.
- **Never tested on the author's PC.** Unit tests use the in-memory/dry-run machines only; the real-machine verification is the user's, in their VM. The deliverable includes a fresh build published to `vm-build\AkariDash-Debug` and a VM-CHECK checklist, matching the repo's pattern.

## Non-goals and anti-goals

- No new Category, page, or Group. No changes to how safe-mode Tweaks appear in the UI.
- Not cleaning up AkariOS's hacks. The user explicitly chose the faithful port over dropping the DISM feature disable and the `smartscreen.exe` move. They are included, described honestly, and the description says plainly what does not come back.
- Not touching `security.defender-optimize`, the other Security rows, or any existing behaviour. This Tweak shares targets with them on purpose; nothing about them changes.
- No new CONTEXT.md vocabulary. This is a Tweak like any other.

## Constraints

- Windows only lets Defender's own keys (`SOFTWARE\Microsoft\Windows Defender\*`), the kernel and DeviceGuard keys, and Defender's services and drivers be written in safe boot as TrustedInstaller. The Tweak's Activation is `InSafeMode`; those targets are not `WrittenInOwnSession`.
- Current-user values are written in the asking session (`WrittenInOwnSession`), because safe mode may sign in as a different account.
- Source of truth for every key, value name and data: `Tweaks/Advanced.ps1` lines 3–435 (MIT), under ADR-0003's rules. ADR-0005 applies: the row exists because it is a decision worth offering, and the source only says how each setting is written.

## The Tweak, target by target

Grouped by where they are written. "Off" and "On" are the Option values. AbsentMeans is what a missing value counts as. The registry half reuses the locations (and AbsentMeans) `security.defender-optimize` already defined wherever they are the same, so the two Tweaks and the single-setting rows share Original Values per location.

### Written by the safe-mode pass (as TrustedInstaller)

**Defender's own settings** (key `SOFTWARE\Microsoft\Windows Defender`, AbsentMeans = the On value):

| Target | Off | On |
|---|---|---|
| `Real-Time Protection\DisableRealtimeMonitoring` | 1 | 0 |
| `Real-Time Protection\DisableAsyncScanOnOpen` | 1 | 0 |
| `Spynet\SpyNetReporting` | 0 | 2 |
| `Spynet\SubmitSamplesConsent` | 0 | 1 |
| `Features\TamperProtection` | 4 | 5 |
| `Windows Defender Exploit Guard\Controlled Folder Access\EnableControlledFolderAccess` | 0 | 1 |
| (key root) `PUAProtection` | 0 | 1 |
| (key root) `VerifiedAndReputableTrustModeEnabled` | 0 | 1 |
| (key root) `SmartLockerMode` | 0 | 1 |

**Smart App Control** (`SYSTEM\CurrentControlSet\Control\AppID\Configuration\SMARTLOCKER`, `...\CI\Policy`):

| Target | Off | On |
|---|---|---|
| `SMARTLOCKER\START_PENDING` | 0 | 4 |
| `SMARTLOCKER\ENABLED` (binary, first byte 0/4) | off | on |
| `CI\Policy\VerifiedAndReputablePolicyState` | 0 | 1 |

Smart App Control cannot truly come back once off — Windows marks it off for good. On writes the values and Windows refuses to act on them; the description says so, exactly as `security.defender-optimize`'s already does.

**Windows Security's notifications, the firewall's and SmartScreen** — same targets as the existing rows, Off = their Off values, On = their defaults:

| Target | Off | On |
|---|---|---|
| Security Center\Notifications + Virus and threat protection (4 values, HKLM) | 1 | 0 |
| `SharedAccess\Epoch` | 1231 | 1228 |
| Firewall `DomainProfile`/`PublicProfile`/`StandardProfile` DisableNotifications | 1 | 0 |
| `Explorer\SmartScreenEnabled` | "Off" | "Warn" |

**Core isolation, SmartScreen for Edge and Store apps, exploit mitigations** — same targets as `security.memory-integrity`, `security.vbs`, `security.lsa-protection`, `security.driver-blocklist`, `security.smartscreen-edge`, `security.smartscreen-store`, `security.exploit-protection`:

| Target | Off | On |
|---|---|---|
| Edge `SmartScreenEnabled` / `SmartScreenPuaEnabled` (HKCU, own session) | 0 | 1 |
| `AppHost\EnableWebContentEvaluation` (HKCU, own session) | 0 | 1 |
| `Session Manager\kernel\MitigationOptions` | adv-defender's mask, `Advanced.ps1:104` (`222222000001000000000000000000000000000000000000`) | null — Windows default (the existing target's AbsentMeans mask) |
| `HypervisorEnforcedCodeIntegrity\Enabled` | 0 | 1 |
| `HypervisorEnforcedCodeIntegrity\WasEnabledBy` | null | 2 |
| `HypervisorEnforcedCodeIntegrity\ChangedInBootCycle` | null | null |
| `DeviceGuard\EnableVirtualizationBasedSecurity` | null (delete) | 1 |
| Boot settings `hypervisorlaunchtype` / `isolatedcontext` / `allowedinmemorysettings` | null | Auto / null / null |
| `Lsa\RunAsPPL` | 0 | 2 |
| `CI\Config\VulnerableDriverBlocklistEnable` | 0 | 1 |

**The services and drivers** — eleven targets, written as their registry `Start` values, not as `ServiceLocation` targets. Why: AkariOS writes these in safe boot as TrustedInstaller by registry (`reg add ... Services\WinDefend /v Start /d 4`), and that is the only write that covers all eleven — a Service Control Manager write refuses protected services such as WinDefend and cannot reach the per-user service template `webthreatdefusersvc` at all, while reading the registry works as administrator for every one of them. On values are AkariOS's enable list; Off is 4 (Disabled); AbsentMeans is the On value, so an edition without the service (e.g. `Sense` on Home) resolves to On rather than reading Custom.

| Service | On (Windows default) | Off |
|---|---|---|
| `WdNisSvc` (network inspection) | 3 (Manual) | 4 |
| `WinDefend` (antivirus) | 2 (Automatic) | 4 |
| `MDCoreSvc` (Defender core) | 2 | 4 |
| `wscsvc` (Security Center) | 2 | 4 |
| `webthreatdefsvc` (web threat defense) | 3 | 4 |
| `webthreatdefusersvc` (per-user template) | 2 | 4 |
| `Sense` (Defender ATP) | 3 | 4 |
| `SecurityHealthService` (Windows Security) | 2 | 4 |
| `WdBoot` (boot driver) | 0 (Boot) | 4 |
| `WdFilter` (mini-filter driver) | 0 | 4 |
| `WdNisDrv` (network inspection driver) | 3 | 4 |

`DelayedAutostart` is never touched, as AkariOS leaves it too.

### Written in the asking session (admin, normal boot)

| Target | Off | On |
|---|---|---|
| The three Defender right-click handlers — default value of `HKLM\SOFTWARE\Classes\{Directory,Drive,*}\shellex\ContextMenuHandlers\EPP` | null (delete) | `"{09A47860-11B0-4DA5-AFA5-26D86198A780}"`, AbsentMeans the same |
| `HKLM\...\CurrentVersion\Run\SecurityHealth` (the tray) | null (delete) | ExpandString `"%windir%\system32\SecurityHealthSystray.exe"`, AbsentMeans the same |
| The four Defender scheduled tasks (as `security.defender-tasks`) | Off | On |
| The three current-user Account protection values (as `security.defender-optimize`) | 1 | 0 |

### Ride-alongs, written by the companion but never read back

The same two `DefenderOptimize.Extra` ride-alongs apply to this Tweak as well, with Off/On in place of Optimize/Undo: the five `WTDS\Components` phishing values (0/1 — the key is unreadable by anyone but SYSTEM and TrustedInstaller) and the Exploit Guard MDM policy refresh task (Off/On).

Plus the two faithful-port pieces, both companion-side, both **best-effort** (a failure is logged and the pass still counts as applied — on Windows 11 the DISM feature does not exist and AkariOS's script ignores the failure the same way):

- **DISM feature** `Windows-Defender-Default-Definitions`: `Dism /Online /NoRestart /Disable-Feature` on Off. **Not done on On** — Windows cannot re-enable it, which the description says.
- **`smartscreen.exe` move**: Off moves `System32\smartscreen.exe` to `C:\Windows\` (overwrite, TrustedInstaller rights, safe mode so it is not running); On moves it back. Stopped-process handling is not needed in safe mode.

## What has to be generalized

The safe-mode machinery currently assumes exactly one safe-mode Tweak. It is about to have two, and a user can stage one while the other is still pending — today the second pass file would overwrite the first and the first Tweak would read applied without its pass having run. So the pass exchange becomes a **queue of pending passes** in the one file (`safe-mode-pass.json`), with the lifecycle settled as follows:

- The file holds one entry per pending pass, and only pending ones: the app appends a new entry and, when the same TweakId already has a `Scheduled` entry in the file, replaces it — the user's latest choice for that Tweak wins (they applied Off, then switched to On, then undid: only the last of those runs).
- The companion executes every entry still `Scheduled`, in order, and records each outcome against that entry; both `Scheduled` entries it has consumed and its own leftovers are handled by the app reading and pruning — concretely: an entry whose Stage is no longer `Scheduled` is an **answer waiting to be read**, not pending work. Entries the app staged for a Tweak it has since changed are replaced at staging time (previous bullet), so a stale never-run entry cannot survive long.
- When staging, the app's rollback deletes only the entry it just staged; another Tweak's pending pass is untouched.
- A file holding a single record still reads as a one-item list, so nothing breaks mid-way.
- No Recommended Option matters here; nothing else consumes the queue.
- `PassTargets` (the own-session filter) and the option lookup by `TweakId` in the companion are already generic; the `DefenderOptimize.Extra(...)` call in the companion becomes a per-`TweakId` dispatch between it and the new Tweak's ride-alongs.
- Companion cosmetics follow the pass (its window title and log lines name the Tweak it is working on, not "Defender optimize" always). The RunOnce value name is cosmetic too — leave it or rename it to something neutral; the builder's call.
- **Failure accounting in the companion** (settled after the builder check): the best-effort steps — DISM on this Tweak, the `smartscreen.exe` move on this Tweak — get their own channel: every outcome is logged, and none of them ever fails the pass or blocks an Undo from clearing the Original Values. The Tweak's Undo is defined by its targets; a best-effort piece that could not be restored (e.g. `C:\Windows\smartscreen.exe` was never moved because Off never ran) is a log line, not a wedged record. The existing phishing-values and ExploitGuard-task ride-alongs keep today's behaviour exactly: a failure there fails the pass, and a failure on Undo keeps the Original Values. The best-effort steps run unconditionally, as AkariOS's script runs DISM regardless of what else failed.

## Description (draft, for the builder to finalize in the repo's voice)

> AkariOS-Ultimate's Advanced Defender switch, the whole of it: real-time, dev drive and cloud protection off, tamper protection off, Controlled Folder Access off, and Defender's own eight services and three drivers disabled, so Windows Defender and Windows Security's tray and right-click entries are gone from the PC. It also takes with it everything the rows above cover — SmartScreen for apps, Edge and Store apps, the system-wide exploit mitigations, memory integrity, virtualization-based security, LSA protection, the vulnerable driver blocklist, Windows Security's and the firewall's notifications and Defender's own scheduled tasks — and puts all of that back with On. Windows only lets Defender's keys be written in safe boot, so applying or undoing it restarts the PC into safe mode and back. Two pieces do not come back: disabling Windows-Defender-Default-Definitions through DISM cannot be undone by anything, and Smart App Control cannot be turned back on without reinstalling Windows, On's writes included. With Defender's services off, Windows Security can no longer report on this PC and no antivirus is running; undoing brings both back.

## What is left to the builder

- Exact code shape of the pass-list change, the per-Tweak extras dispatch, and where the new Tweak's targets live in `TweakCatalog.Security.cs` (a `DisableDefender()` method beside `DefenderOptimizeTweak()`, following its style, is the obvious shape but not mandated).
- Helper factories for the ExpandString and default-value registry targets if the existing ones do not cover them.
- Test breakdown following the existing suites (`DefenderOptimizeTests`, `SecurityCatalogTests`, `TweakEngine*`), including asserting the pass targets exclude own-session targets and the companion ride-alongs for both Tweaks.
- The VM-CHECK checklist file, named like the others in `vm-build\` (`VM-CHECK-DISABLE-DEFENDER.md`).
- The publish command for the VM build: the same steps as `installer\Build-Installer.ps1` but Debug, output `vm-build\AkariDash-Debug`, companion exe included.

## Decisions already made, as they were made

- Faithful full port including DISM and the smartscreen move — the user's explicit choice, over the softer alternative.
- Security page, "Microsoft Defender" group, titled "Disable Defender" — the user's choice.
- **My call, flagged:** On restores virtualization-based security and its hypervisor boot settings, which AkariOS's enable list does not (it leaves VBS off). An "On" that leaves WSL2, Hyper-V and Windows Sandbox broken after the user chose to enable is a footgun, and the existing `security.vbs` row's "Let Windows decide" is exactly the state On writes. Everything else in On is AkariOS's enable list verbatim. If this is wrong for the faithful port, On drops those four values and the description points at the VBS row instead.
- **My call:** the eleven services and drivers are registry `Start` targets rather than `ServiceLocation` targets, for the reasons under that table.
- **My call:** On leaves `MitigationOptions` absent (Windows default) rather than writing AkariOS's explicit default mask — the same state either way, and it matches the existing `security.exploit-protection` row's "Windows default".
