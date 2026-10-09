namespace AkariDash.Core.Tweaks;

/// <summary>Persists the <see cref="AppliedTweak"/> record of every Tweak Akari-Dash has applied.</summary>
public interface IOriginalValuesStore
{
    /// <summary>The record for <paramref name="tweakId"/>, or <see langword="null"/> when it has not been applied.</summary>
    AppliedTweak? Get(string tweakId);

    void Save(string tweakId, AppliedTweak applied);

    void Clear(string tweakId);
}
