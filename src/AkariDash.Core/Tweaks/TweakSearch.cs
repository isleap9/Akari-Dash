using AkariDash.Core.Machine;

namespace AkariDash.Core.Tweaks;

/// <summary>
/// One Tweak a search found. <see cref="Detail"/> says where it lives, so two Tweaks with similar
/// titles can be told apart, and names the value that matched when the match was in one.
/// </summary>
public sealed record TweakMatch(DeclaredTweak Tweak, string Detail);

/// <summary>
/// Finds a Tweak by what the user can see of it (its title, Group, Category, Option labels and
/// description) and by what it actually writes (a registry value name, a service, a scheduled task),
/// so "SysMain", "SilentInstalledAppsEnabled" and "superfetch" all lead somewhere.
/// </summary>
public static class TweakSearch
{
    // Lower is a better match. Where the query turned up decides the order, so a Tweak whose title
    // starts with it comes before one that merely mentions it in its description.
    private const int TitleStart = 0;
    private const int TitleWord = 1;
    private const int Title = 2;
    private const int GroupOrCategory = 3;
    private const int OptionOrId = 4;
    private const int Target = 5;
    private const int Description = 6;
    private const int EveryWordSomewhere = 7;

    /// <summary>
    /// The best matches for <paramref name="query"/>, up to <paramref name="limit"/>. An empty or
    /// whitespace query finds nothing: a search box with nothing typed has nothing to suggest.
    /// </summary>
    public static IReadOnlyList<TweakMatch> Find(
        string? query, IEnumerable<DeclaredTweak>? catalog = null, int limit = 12)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return [];
        }

        var trimmed = query.Trim();
        var words = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return (catalog ?? TweakCatalog.All)
            .Select(tweak => (tweak, rank: Rank(tweak, trimmed, words)))
            .Where(found => found.rank.Score < int.MaxValue)
            .OrderBy(found => found.rank.Score)
            .ThenBy(found => found.tweak.Category)
            .ThenBy(found => found.tweak.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(found => found.tweak.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(found => new TweakMatch(found.tweak, Detail(found.tweak, found.rank.MatchedTarget)))
            .ToList();
    }

    /// <summary>Where a Tweak sits, and the value that matched when the query was a value's name.</summary>
    private static string Detail(DeclaredTweak tweak, string? matchedTarget)
    {
        var where = $"{tweak.Category} > {tweak.Group}";

        return matchedTarget is null ? where : $"{where}  ·  {matchedTarget}";
    }

    private static (int Score, string? MatchedTarget) Rank(DeclaredTweak tweak, string query, string[] words)
    {
        if (tweak.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return (TitleStart, null);
        }

        if (WordStartsWith(tweak.Title, query))
        {
            return (TitleWord, null);
        }

        if (Has(tweak.Title, query))
        {
            return (Title, null);
        }

        if (Has(tweak.Group, query) || Has(tweak.Category.ToString(), query))
        {
            return (GroupOrCategory, null);
        }

        if (tweak.Options.Any(option => Has(option.Label, query)) || Has(tweak.Id, query))
        {
            return (OptionOrId, null);
        }

        if (TargetNames(tweak).FirstOrDefault(name => Has(name, query)) is { } target)
        {
            return (Target, target);
        }

        if (Has(tweak.Description, query))
        {
            return (Description, null);
        }

        // Several words, each somewhere in the Tweak: "lock screen tips", "xbox service".
        if (words.Length > 1)
        {
            var haystack = string.Join(
                '\n',
                [tweak.Title, tweak.Group, tweak.Category.ToString(), tweak.Description, tweak.Id,
                 .. tweak.Options.Select(option => option.Label), .. TargetNames(tweak)]);

            if (words.All(word => Has(haystack, word)))
            {
                return (EveryWordSomewhere, null);
            }
        }

        return (int.MaxValue, null);
    }

    /// <summary>What each target is called where Windows keeps it, which is what a power user types.</summary>
    private static IEnumerable<string> TargetNames(DeclaredTweak tweak) =>
        tweak.Targets.Select(target => target.Location switch
        {
            RegistryLocation registry => registry.Name,
            RegistryEntryLocation entry => entry.Entry,
            RegistryKeyLocation key => key.Key[(key.Key.LastIndexOf('\\') + 1)..],
            RegistryBitLocation bit => bit.Name,
            ServiceLocation service => service.ServiceName,
            ScheduledTaskLocation task => task.Path,
            BootSettingLocation boot => boot.Name,
            BlackImageLocation image => Path.GetFileName(image.Path),
            AccountPicturesLocation => "User Account Pictures",
            var other => other.ToString() ?? string.Empty,
        });

    private static bool Has(string text, string query) =>
        text.Contains(query, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether any word of <paramref name="text"/> starts with the query ("hover" finds "Mouse hover time").</summary>
    private static bool WordStartsWith(string text, string query)
    {
        var at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        while (at > 0)
        {
            if (!char.IsLetterOrDigit(text[at - 1]))
            {
                return true;
            }

            at = text.IndexOf(query, at + 1, StringComparison.OrdinalIgnoreCase);
        }

        return at == 0;
    }
}
