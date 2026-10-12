namespace AkariDash.Core.Machine;

/// <summary>No restore point could be made because System Protection is off for the PC's drives.</summary>
public sealed class SystemProtectionOffException()
    : InvalidOperationException("System Restore is turned off on this PC (System Protection is off for its drives).");
