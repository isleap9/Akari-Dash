namespace AkariDash.Core.Machine;

/// <summary>
/// A solid black image file, which AkariOS-Ultimate's sign-out wallpaper points the lock screen and the
/// desktop at (C:\Windows\Black.jpg). Writing <see cref="BlackImage.Black"/> draws the file; deleting
/// removes it, so an Original Value of "did not exist" is undone by taking the file away again.
/// </summary>
public sealed record BlackImageLocation(string Path) : MachineLocation;

/// <summary>
/// The default account pictures under ProgramData\Microsoft\User Account Pictures, which
/// AkariOS-Ultimate paints black after backing them up to ProgramData\User Account Pictures. Writing
/// <see cref="BlackImage.Black"/> does the same; deleting puts the backup back and removes it. They
/// read as black while that backup exists, the mark AkariOS-Ultimate's own revert relies on.
/// </summary>
public sealed record AccountPicturesLocation : MachineLocation
{
    public static AccountPicturesLocation Default { get; } = new();
}

/// <summary>What a <see cref="BlackImageLocation"/> or <see cref="AccountPicturesLocation"/> holds when painted black; otherwise <see langword="null"/>.</summary>
public sealed record BlackImage : MachineValue
{
    public static BlackImage Black { get; } = new();

    private BlackImage()
    {
    }
}
