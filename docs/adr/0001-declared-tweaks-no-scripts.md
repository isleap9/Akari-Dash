# Tweaks are declared as data, never run as scripts

A Tweak is declared as data (registry values, single bits of a binary registry value, boot settings, service start types, scheduled tasks) and executed by one shared engine; only a Tweak that cannot be expressed as data becomes a hand-written Coded Tweak in C#. PowerShell scripts are never used, because earlier attempts (AkariOS-Companion) mixed scripts with registry code, which made state impossible to read back, undo reliably, or test.

v1 ships without a Coded Tweak contract: every v1 Tweak can be declared as data, so the engine handles Declared Tweaks only. The contract is added together with the first Tweak that actually needs code, so its shape comes from a real case rather than a guess.

_Amended 2026-10-12 (#23): a single bit of a binary registry value, such as an effect in UserPreferencesMask, is also data. Reading or writing it touches only that bit, so it keeps Live State, Original Values and Undo exact without any code per Tweak._

_Amended 2026-10-12 (#26): a boot setting of the current Windows entry (bcdedit {current}) is data too, read and written with System32's bcdedit. A missing one means Windows' default, like a missing registry value._

_Amended 2026-10-12: a few registry keys belong to a Windows service, and Administrators may only read them; Settings changes them through the service. A value under such a key is still data. WindowsMachine writes it with the restore privilege every administrator has, as backup tools do, and only for keys on its explicit ServiceOwnedKeys list (so far Windows Search's SystemIndex, for Find my files). The key's permissions are never changed, and nothing runs as SYSTEM or TrustedInstaller._
