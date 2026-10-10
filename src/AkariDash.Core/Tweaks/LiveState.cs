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

    /// <summary>
    /// Akari-Dash applied <paramref name="Expected"/> but the machine is no longer in it (typically
    /// Windows reset it); <paramref name="Actual"/> is the <see cref="InOption"/> or <see cref="Custom"/>
    /// state it is in instead. Shown in place of Custom whenever Akari-Dash has applied the Tweak.
    /// </summary>
    public sealed record Drifted(TweakOption Expected, LiveState Actual) : LiveState;

    /// <summary>
    /// The Tweak cannot be used on this machine (a target is missing or the hardware does not
    /// match); <paramref name="Reason"/> says why. Shown in place of every other state.
    /// </summary>
    public sealed record Unavailable(string Reason) : LiveState;
}

/// <summary>The live value of one target (<see langword="null"/> when it does not exist).</summary>
public sealed record TargetValue(TweakTarget Target, MachineValue? Value);
