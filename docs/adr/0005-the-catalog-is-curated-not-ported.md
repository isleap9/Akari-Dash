# The catalog is curated, not ported: a source says how to write a setting, never which settings to have

Amends [0003](0003-tweak-content-mit-sources-only.md), which said Winhance "has the largest list of settings, so it is the main starting point for which Tweaks to add and the values they write". The second half stands. The first half is withdrawn.

A **Tweak** ships because it is a decision worth offering on a gaming PC. It does not ship because a source happens to have the value. Those are different tests, and the catalog had been using the wrong one: with a source's list in hand, the work becomes transcription, and the result is a registry browser wearing Akari-Dash's paint.

So the sources change jobs:

- **Which Tweaks exist** is Akari-Dash's decision, made one row at a time. The question is whether a user of this app would want that choice, not whether anyone else offers it.
- **How a setting is written** — the key, the value name, the data, what a missing value means — still comes from Microsoft's documentation, Akari-OS / AkariOS-Ultimate, or Winhance's MIT snapshot, under 0003's licence rules. Nothing about that changes.
- **Groups and pages** are ours. A Group is a cluster a user would look for ("What the taskbar shows"), never a mirror of a source's file layout or its own section names.

One **Tweak** is one decision, which is already what `CONTEXT.md` says and what the import tickets asked for. A page with one row per registry value is the failure this ADR exists to stop: thirty-four rows of Folder Options checkboxes is not thirty-four decisions, it is one source's settings dialog.

## What this reverses

Porting every row a source offers, and asking before dropping one. That had it backwards: leaving a row out is the normal case now and needs no ceremony, while putting one in needs a reason that survives being written down in its description.

It does **not** reverse the same rule applied *inside* a Tweak. Once a Tweak ships, it ships every state the machine really has: the four taskbar search modes, the six UAC levels. Half a Tweak is a worse lie than no Tweak, because the user sees the choice and cannot make it.

A source's *preset* is not a state, though. An Option that only sets values other rows already own is a shortcut, not a choice the machine otherwise lacks, and it can be left out like any row. That is why `update.policy` offers Normal and AkariOS-Ultimate's pause only: Winhance's Security updates only and Disabled were presets over the rows below it, and every machine state they reached is still one row away.

## The cost, accepted

A smaller catalog. Customize's Explorer page goes from the 34 rows a port would have produced to the few that change how a gaming PC is actually used. Some settings a competitor lists will not be here, and "Winhance has it" is not an argument for adding one.

The `// Left out:` comments at the top of each catalog file, with the reason and a test asserting the id is absent, stay exactly as they are. They are how a decision not to ship stays visible instead of looking like an oversight.

_Decided 2026-10-13, mid-#32, when the Explorer page's 34-row plan made the problem obvious enough to name._

_Amended 2026-10-19 (#33): the Drivers page is ported, not curated — AkariOS-Ultimate's Graphics category, every entry, at the user's call (see ADR-0001's amendment of the same date). C++ redistributables, and the links to Settings' display and HAGS pages, are there too, as AkariOS has them, even though Downloads and Gaming touch the same ground._
