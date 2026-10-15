namespace AkariDash.Core.Drivers;

/// <summary>The driver a vendor says is current for a card, as the vendor itself reports it.</summary>
public sealed record AvailableDriver(string Version, Uri DownloadUrl);
