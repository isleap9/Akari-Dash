# Tweaks come from MIT sources only: Akari-OS, and Winhance up to its last MIT commit

Tweaks are written from the Akari-OS / FR33THY scripts (MIT), public Windows documentation, and Winhance as of commit 4dc441a0 (2026-02-10), the last version released under the MIT License. From v26.02.13 (commit 23cb77d0) on, Winhance is licensed PolyForm Shield 1.0.0, which forbids using it in a competing product, and Akari-Dash is one. Nothing newer than 4dc441a0 is used, not even as an index of ideas. AkariOS-Companion and Akari-Tool are based on Shield-era Winhance, so they are not sources either.

Winhance has the largest list of settings, so it is the main starting point for the values a Tweak writes. (It was also the starting point for *which* Tweaks to add; [0005](0005-the-catalog-is-curated-not-ported.md) withdraws that, and leaves the rest of this ADR standing.) Its code is never copied 1:1: every Tweak is rewritten as an Akari-Dash Declared Tweak with its own id, title, description, Options, Recommended Option and Group, and a value is dropped when it cannot be undone (as with Recall's AllowRecallEnablement). The MIT notices are kept in `THIRD-PARTY-NOTICES.md`.

_Amended 2026-10-12. This ADR first said Winhance could only be read as an index of ideas, before the MIT snapshot was found._

_Amended again 2026-10-12: newer, PolyForm Shield versions of Winhance may be read for one purpose only, to list which settings exist (their names). Nothing else is taken from them: every value comes from Microsoft's documentation, Akari-OS / AkariOS-Ultimate or Windows itself, and a setting without such a source is left out._
