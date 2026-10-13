# Akari-Dash

A Windows desktop dashboard that optimizes a PC for gaming by applying, reading back, and undoing system changes.

## Language

**Tweak**:
One named change to Windows that Akari-Dash can apply and undo as a unit: if any part of an apply fails, the parts already written are rolled back to what they held just before that apply (their **Original Values** on a first apply), so a failed **Option** switch leaves the Tweak in the Option it was in.
_Avoid_: Setting, optimization, script, action

**Declared Tweak**:
A **Tweak** described entirely as data (registry values, single bits of a binary registry value, boot settings, service start types, scheduled tasks) and executed by the shared engine.
_Avoid_: Registry tweak, simple tweak

**Coded Tweak**:
A **Tweak** whose apply and read-back logic is hand-written because it cannot be expressed as data.
_Avoid_: Custom tweak, native tweak, script tweak

**Action**:
Something Akari-Dash does once that it cannot undo: installing or removing an app, downloading and installing a driver, resetting a graphics driver. The mirror of a **Tweak** — no **Original Values**, no **Undo**, no **Recommended Option** — but it still reads back whether it has been done (is this app installed?), reports progress while it runs, keeps a log, and says plainly before it starts that it cannot be taken back. An Action never pretends to be a Tweak, and a change that *can* be undone is never an Action.
_Avoid_: Task, job, operation, script, tool

**Option**:
One named state a **Tweak** can be put in; every Tweak has two or more, and an on/off Tweak is simply the two-Option case.
_Avoid_: Value, mode, choice, toggle state

**Recommended Option**:
The **Option** Akari-Dash suggests for a **Tweak** on performance grounds; "apply all recommended" puts every Tweak that has one into it. A Tweak that is a matter of taste has none and is skipped.
_Avoid_: Default, preset, optimal value

**Live State**:
Which **Option** the machine is actually in right now, read fresh from the system rather than from Akari-Dash's own records. A missing value counts as whatever Windows does when it is absent (e.g. Game Mode is on when its value does not exist). When Akari-Dash has applied the Tweak, its records are used only to tell **Drift** apart from **Custom**.
_Avoid_: Status, current value, saved state

**Custom**:
A **Live State** that matches none of a **Tweak**'s **Options** and was not caused by Akari-Dash applying *this* Tweak; shown with the values that differ from the **Option** it last applied, or with all of them when it has applied none. A Tweak two Tweaks share a value with reads Custom, not **Drift**, once the other one is applied over it.
_Avoid_: Unknown, invalid, Drift

**Unavailable**:
A **Tweak** whose targets do not exist on this machine (missing hardware, service, or Windows feature); shown with the reason and skipped by "apply all recommended".
_Avoid_: Hidden, unsupported, disabled

**Activation**:
When a **Tweak**'s change actually takes effect: immediately, after sign-out, or after restart.
_Avoid_: Reboot required, apply mode

**Category**:
A top-level area of the dashboard (e.g. Gaming, Privacy) that holds **Groups**.
_Avoid_: Page, section, tab

**Group**:
A named cluster of related **Tweaks** inside a **Category** (e.g. Game Mode, Network).
_Avoid_: Subcategory, block, card

**Original Values**:
The machine's own values a **Tweak** touches, saved once, just before Akari-Dash first applies it, and kept unchanged until **Undo**; re-applying or switching **Options** never overwrites them. Undo restores exactly these. "Did not exist" is a valid Original Value, restored by deleting.
_Avoid_: Default, backup, Windows default, snapshot

**Undo**:
Returning a **Tweak**'s targets to their **Original Values**. A change that cannot be undone is not a Tweak.
_Avoid_: Revert, restore, reset, rollback

**Dry Run**:
An apply that reads the real machine but writes nothing, recording exactly what it would have changed instead. Users see it as the "what will change" preview before applying.
_Avoid_: Simulation, mock mode, preview mode

**Drift**:
When a **Tweak**'s **Live State** no longer matches the **Option** Akari-Dash last applied *and nothing Akari-Dash did explains it* — so, because Windows reset it. Two Tweaks may write one value on purpose (an update policy mode covers the rows beneath it), and applying one of them is not the other drifting: Akari-Dash moved it, on the user's instruction, and offering to re-apply would undo the choice they just made. That reads as **Custom**, with **Undo** intact.
_Avoid_: Reverted, out of sync, broken

**Restore Point**:
A Windows System Restore point Akari-Dash asks for once per session, just before its first real (non-**Dry Run**) apply; a safety net for the whole PC, separate from **Undo** and **Original Values**. If Windows cannot create one, the user is warned and the apply goes ahead.
_Avoid_: Backup, snapshot, checkpoint
