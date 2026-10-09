namespace AkariDash.Core.Tweaks;

/// <summary>Keeps Original Values for this session only: used by Phase 1 builds and tests.</summary>
public sealed class InMemoryOriginalValuesStore : IOriginalValuesStore
{
    private readonly Dictionary<string, AppliedTweak> _applied = [];

    public AppliedTweak? Get(string tweakId) => _applied.GetValueOrDefault(tweakId);

    public void Save(string tweakId, AppliedTweak applied) => _applied[tweakId] = applied;

    public void Clear(string tweakId) => _applied.Remove(tweakId);
}
