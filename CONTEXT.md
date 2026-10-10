# Akari-Dash

A Windows desktop dashboard that optimizes a PC for gaming by applying, reading back, and undoing system changes.

## Language

**Tweak**:
One named change to Windows that Akari-Dash can apply and undo as a unit: if any part of an apply fails, the parts already written are rolled back to what they held just before that apply (their **Original Values** on a first apply), so a failed **Option** switch leaves the Tweak in the Option it was in.
_Avoid_: Setting, optimization, script, action

**Declared Tweak**:
A **Tweak** described entirely as data (registry values, service start types, scheduled tasks) and executed by the shared engine.
_Avoid_: Registry tweak, simple tweak

**Coded Tweak**:
A **Tweak** whose apply and read-back logic is hand-written because it cannot be expressed as data.
_Avoid_: Custom tweak, native tweak, script tweak

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
A **Live State** that matches none of a **Tweak**'s **Options** and was not caused by Akari-Dash; shown with the actual value.
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
When a **Tweak**'s **Live State** no longer matches the **Option** Akari-Dash last applied, typically because Windows reset it.
_Avoid_: Reverted, out of sync, broken
