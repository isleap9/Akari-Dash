namespace AkariDash.Core.Tweaks;

/// <summary>When a Tweak's change actually takes effect.</summary>
public enum Activation
{
    Immediately,
    AfterSignOut,
    AfterRestart,

    /// <summary>
    /// Written on the next restart, in safe mode, by the companion exe beside the app: Windows only
    /// lets Defender's own keys be written there, as TrustedInstaller. Applying or undoing such a
    /// Tweak stages a pass and restarts the PC, so the change is in place when the user is back.
    /// </summary>
    InSafeMode,
}
