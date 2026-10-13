# Akari-Dash covers Tweaks and Actions; live monitoring and stress tests stay out

Supersedes [0002](0002-system-management-out-of-v1.md).

Akari-Dash is a gaming dashboard and a Windows optimizer. It covers four things: the **Tweak** catalog (gaming, privacy, security, services, Windows, update policy, GPU settings), **Customize** (Explorer, Taskbar, Start Menu), **Apps** (installing and removing software, including the Windows apps a machine ships with), and **GPUs** (installing drivers, and resetting them with DDU).

Apps and GPU driver work are not Tweaks and never become them. They are **Actions**: done once, not undoable, with progress and a log and a warning before they start. 0002 excluded them on the grounds that "most have no readable state and no undo", and the second half of that is still true — an uninstall cannot be taken back. What changed is the answer: rather than leaving them out, they get their own concept with its own, weaker, clearly-stated promise, which is what 0002 itself said would have to happen. The Tweak engine's guarantee is untouched: everything the engine applies is still read back and undoable, and nothing that is not undoable is ever shown as a Tweak.

Still out, for the same reason 0002 gave — these are what killed earlier Akari projects, and neither is a change to the machine that anyone can undo:

- **Live monitoring**: temperature, usage and FPS overlays, anything that samples the machine continuously.
- **Stress tests and benchmarks.**

Windows Update policy, installers and driver tools, which 0002 excluded by name, are in. Update policy is ordinary registry data and reads back and undoes like any other Tweak. Installers and driver tools are Actions.

_Decided 2026-10-13, after a design session that worked through what the app is for. The scope is wider on purpose, and written down here rather than arrived at by drift._
