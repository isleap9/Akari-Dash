# VM check: Disable Defender (Security page, Microsoft Defender)

VM only, never on the host. App: `vm-build\AkariDash-Debug\Akari Dash.exe`. The companion,
`AkariDash.DefenderReset.exe`, must sit in that same folder — the app looks for it beside itself and
stages nothing without it. This is the real Debug build: Off really does turn Windows Defender off,
and the PC really does restart into safe mode and back. **Take a snapshot first.**

**If the PC is ever stuck in safe mode:** open an administrator Command Prompt (Ctrl+Shift+Esc >
File > Run new task > `cmd`, tick "administrator"), run `bcdedit /deletevalue {current} safeboot`,
then `reg delete HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce /v *defenderoptimize /f`,
and restart. Or restore the snapshot.

## What you are testing

The row is **Security > Microsoft Defender > Disable Defender**. It has two buttons, **Off** and
**On**. Off is AkariOS-Ultimate's Advanced "Defender" disable list; On is its enable list. Both are
written in safe mode, as TrustedInstaller, because Windows only lets Defender's own keys, the kernel
and DeviceGuard keys, and Defender's own services be written there. Applying either one asks to
restart; the row reads **Off** while Defender is off and **On** once it is back.

`vm-build\Check-Defender.ps1` (read-only, run it elevated) prints which Defender processes are
running, each service's `Start` value, and whether Akari-Dash ever saved Original Values for any of
them. It takes no arguments — it just prints and copies its output to the clipboard, so it is worth
running at every step.

1. **Apply Off, and watch it stage.** On first launch Akari-Dash asks once whether to create a System
   Restore point (and, if System Protection is off, whether to turn it on and create one); after that
   the session's first apply waits for one and an info bar says so. Security > Microsoft Defender >
   Disable Defender, press **Off**. The dialog lists what will change — a long list, because the row shares
   most of the Security page's targets on purpose — and says the change is made in safe mode. Press
   **Apply**. The info bar reads **"The PC is restarting into safe mode to make N change(s)."**, and
   the PC restarts on its own within about twenty seconds.

   Before it goes, the pass is already written: `C:\ProgramData\Akari-Dash\safe-mode-pass.json` holds
   `security.disable-defender` / `off` with `"Stage": "Scheduled"`, and RunOnce
   (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`, value **`*defenderoptimize`**) points at
   the companion. That is the "restart requested" half — the values themselves are not on the machine
   yet. Parts of the row the app writes itself are already down, though — the tray icon, the
   right-click entries, the four Defender tasks, the account-protection reminders and the Edge/Store
   SmartScreen values, **14 in total** (see **The roster** below).

2. **The safe-mode restart, once.** In safe mode Windows runs the companion from RunOnce, as
   TrustedInstaller. Its window is titled **"Akari Dash - safe mode pass"**. It turns safe mode off for
   the next start first, does every pass waiting at the file, and then restarts — so an apply, and an
   undo, are each a single round trip into safe mode and back. **Off and On behave the same way**: both
   are safe-mode writes, so the same restart serves either; only the values written differ.

   The companion logs to `C:\ProgramData\Akari-Dash\logs\defender-reset.log`. Read it after the
   restart: for an Off apply it holds `Applying security.disable-defender off.`, then a line for each
   value Windows would not take, then `Wrote N value(s).`. An On apply logs
   `Applying security.disable-defender on.`; an undo logs
   `Putting Disable Defender's settings back.`

3. **Off is really off.** Run `Check-Defender.ps1`, then confirm:

   - `sc qc WinDefend` reports **START_TYPE : 4   DISABLED**. All eleven should read 4.
     `Check-Defender.ps1` prints nine of them (`WinDefend`, `MDCoreSvc`, `WdNisSvc`, `Sense`, `wscsvc`,
     `SecurityHealthService`, `WdFilter`, `WdBoot`, `WdNisDrv`); check the other two by hand —
     `reg query "HKLM\SYSTEM\CurrentControlSet\Services\webthreatdefsvc" /v Start` and the same for
     `webthreatdefusersvc` (the per-user template service) — each should read `0x4`.
   - Windows Security shows **no antivirus** and there is **no tray icon** (Defender can no longer
     report on this PC, and the `SecurityHealth` run value is gone).
   - Right-clicking an **.exe** or a **drive** in File Explorer has no **"Scan with Microsoft
     Defender"** entry.
   - `C:\Windows\System32\smartscreen.exe` is **absent**, and `C:\Windows\smartscreen.exe` is
     **present**.
   - `Get-ScheduledTask -TaskPath '\Microsoft\Windows\Windows Defender\*' | Select TaskName, State`
     shows the four tasks **Disabled**.
   - The DISM feature: `Get-WindowsOptionalFeature -Online -FeatureName
     Windows-Defender-Default-Definitions`. On a **Windows 10** VM it reads **Disabled**; on a
     **Windows 11** VM Windows reports the feature does not exist. Both are expected — see step 4.

4. **The two pieces that do not come back — this is why it is a VM check.** Off runs
   `Dism.exe /Online /NoRestart /Disable-Feature /FeatureName:Windows-Defender-Default-Definitions`
   and moves `C:\Windows\System32\smartscreen.exe` to `C:\Windows\smartscreen.exe`. **Neither is
   restored by anything**: Windows cannot re-enable that DISM feature, and On only moves
   `smartscreen.exe` back. On **Windows 11 the feature does not exist, so the DISM call fails every
   time** — that failure must be a line in `defender-reset.log` (it starts
   `Windows-Defender-Default-Definitions: exit ...`), **never a failed pass**. The row still reads
   Off and `safe-mode-pass.json` records `"Stage": "Applied"`. Finding the exit-code line and an
   applied pass is the pass condition; do not read the exit code itself as a problem.

5. **Both safe-mode Tweaks in one restart.** Stage **Security > Microsoft Defender > Defender
   optimize** (press **Optimize**) and then apply **Disable Defender > Off**, so two passes are
   pending in the one file. Restart (the second apply's own restart does it). **Inspect
   `C:\ProgramData\Akari-Dash\safe-mode-pass.json` before reopening Akari-Dash** — the file is the
   companion's own record, and Akari-Dash rewrites the queue (dropping a Tweak's answered entry)
   whenever it stages that Tweak again, so read it while both `Applied` entries are still there. It
   should hold both `security.defender-optimize` and `security.disable-defender`, both **Applied**, and
   the machine holds **both** Tweak's values at once — optimize's Optimize choices and Defender fully
   off. This is the queue doing its job: the second apply must not have
   replaced the first.

6. **On, and Undo, put Defender back.** Press **On** (or **Undo**), agree to the restart, and let it
   run. Both ride the enable side, so either way Defender comes back: the eleven services at their
   Windows defaults, the tray icon and the right-click entries back, `smartscreen.exe` back in
   `System32`, and VBS, memory integrity and the hypervisor boot settings restored. The row reads
   **On**.

   Undo specifically puts back the Original Values Akari-Dash saved the first time the Tweak was
   applied — on a PC that shipped with Defender running, that is Defender back on. It rides the enable
   side whichever Option was applied last (Off, or On), so the ride-alongs are put back on and
   smartscreen returns. The one thing Undo does **not** do is re-enable the DISM feature: Windows
   cannot, the log says so, and the row's description says so too. That single piece stays disabled
   after an undo, and that is correct.

7. **The row's reading, and drift.** The row reads **Off** while Defender is off and **On** once it is
   back. If Windows turns Defender's own scheduled tasks back on by itself (tamper protection does
   this), the row says so honestly — that is the same shared-target behaviour the other Defender rows
   already document, and it is not a bug to fix here.

## The roster: 56, 42, 14

The row holds **56 targets**. Of those, **14** the app writes itself, in the session that asked,
before the restart:

- the tray icon (`HKLM\...\CurrentVersion\Run\SecurityHealth`),
- the three right-click menu entries (the default value of the `EPP` handler under
  `Directory`, `Drive` and `*`),
- the four Defender scheduled tasks under `\Microsoft\Windows\Windows Defender\`,
- the three current-user account-protection reminders, and
- the three current-user Edge/Store SmartScreen values.

It writes those itself because safe mode may sign in as a different account, and a current-user or
normal-session value belongs to whoever is signed in.

The other **42** ride the safe-mode pass and are written by the companion as TrustedInstaller. So
after Apply, but before the restart, the 14 are already on the machine and the 42 are not; after the
restart all 56 are.

Where to look:

- `C:\ProgramData\Akari-Dash\logs\defender-reset.log` — the companion's own log. The
  **`Wrote N value(s).`** line is the pass-side count: the 42 targets, plus the six ride-alongs the
  companion also writes (the five phishing values under `WTDS\Components` and the Exploit Guard MDM
  policy refresh task), less any of those Windows does not have — a boot setting or a task that is not
  there is skipped, not written. A `could not be written` line means a target failed and the pass
  records **Failed** instead of Applied.
- `C:\ProgramData\Akari-Dash\safe-mode-pass.json` — the pass itself, and the Stage the companion
  recorded for it (`Scheduled`, `Applied`, `Failed`).
- `C:\ProgramData\Akari-Dash\original-values.json` — what Undo will put back; the companion clears a
  Tweak's record once a clean undo has restored it.
- `%LocalAppData%\Akari-Dash\logs` — the app's own log (what it previewed and applied, and whether a
  restore point was made).
- `vm-build\Check-Defender.ps1` — the read-only read-back described above.

## When this passes

With Off and back on both clean, the queue carrying two safe-mode Tweaks through one restart, the
Windows 11 DISM failure reading as a log line rather than a failed pass, and Undo restoring Defender
while leaving the DISM feature disabled as documented, the row is good to close.
