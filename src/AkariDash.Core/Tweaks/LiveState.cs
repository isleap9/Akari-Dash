using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>Which Option the machine is actually in right now, read fresh from the system.</summary>
public abstract record LiveState
{
    private LiveState()
    {
    }

    /// <summary>The machine is in <paramref name="Option"/>.</summary>
    public sealed record InOption(TweakOption Option) : LiveState;

    /// <summary>The machine matches none of the Options; <paramref name="Values"/> holds what is actually there.</summary>
    public sealed record Custom(IReadOnlyList<TargetValue> Values) : LiveState;
}

/// <summary>The live value of one target (<see langword="null"/> when it does not exist).</summary>
public sealed record TargetValue(RegistryTarget Target, RegistryValue? Value);
