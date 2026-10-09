# Tweaks are declared as data, never run as scripts

A Tweak is declared as data (registry values, service start types, scheduled tasks) and executed by one shared engine; only a Tweak that cannot be expressed as data becomes a hand-written Coded Tweak in C#. PowerShell scripts are never used, because earlier attempts (AkariOS-Companion) mixed scripts with registry code, which made state impossible to read back, undo reliably, or test.
