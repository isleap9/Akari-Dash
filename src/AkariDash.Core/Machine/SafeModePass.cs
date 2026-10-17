using System.Text.Json;
using System.Text.Json.Serialization;
using AkariDash.Core.Tweaks;

namespace AkariDash.Core.Machine;

/// <summary>How far a safe-mode pass got.</summary>
public enum SafeModePassStage
{
    /// <summary>The app has written the pass and is restarting into safe mode.</summary>
    Scheduled,

    /// <summary>The companion did the work.</summary>
    Applied,

    /// <summary>The companion could not finish it; <see cref="SafeModePass.Message"/> says why.</summary>
    Failed,
}

/// <summary>
/// One Tweak's writes, handed to the companion exe because Windows only lets them be written in safe
/// mode as TrustedInstaller. The app writes this beside the Original Values and restarts; the
/// companion reads it in safe mode, does the work, and writes the outcome back into it.
/// </summary>
/// <param name="TweakId">The Tweak the pass is for.</param>
/// <param name="OptionId">For an apply, the Option to write; for an undo, the Option that was applied.</param>
/// <param name="Undo">
/// <see langword="true"/> puts the Original Values back and forgets them; <see langword="false"/>
/// applies <paramref name="OptionId"/>.
/// </param>
/// <param name="Stage">How far the pass has got.</param>
/// <param name="Message">Why a pass failed, when it did.</param>
public sealed record SafeModePass(
    string TweakId,
    string OptionId,
    bool Undo,
    SafeModePassStage Stage = SafeModePassStage.Scheduled,
    string? Message = null)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Where the app and the companion exchange the pass, beside the Original Values.</summary>
    public static string DefaultPath { get; } =
        Path.Combine(FileOriginalValuesStore.DefaultFolder, "safe-mode-pass.json");

    /// <summary>The pass at <paramref name="path"/>, or <see langword="null"/> when there is none.</summary>
    public static SafeModePass? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SafeModePass>(File.ReadAllText(path), SerializerOptions);
    }

    /// <summary>
    /// Every pass waiting at <paramref name="path"/>, in the order they were staged; nothing when
    /// there is none. A file left by the single-pass app reads as a one-item list.
    /// </summary>
    public static IReadOnlyList<SafeModePass> ReadList(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var text = File.ReadAllText(path);

        // The file used to hold one record and now holds a list; a '[' is what tells them apart.
        return text.TrimStart().StartsWith('[')
            ? JsonSerializer.Deserialize<List<SafeModePass>>(text, SerializerOptions) ?? []
            : JsonSerializer.Deserialize<SafeModePass>(text, SerializerOptions) is { } pass ? [pass] : [];
    }

    /// <summary>Writes this pass, making the folder first when it is not there yet.</summary>
    public void Write(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside and moved into place, so a reader never sees half a file: the app and the
        // companion read this while the other may be writing it.
        var beside = path + ".writing";
        File.WriteAllText(beside, JsonSerializer.Serialize(this, SerializerOptions));
        File.Move(beside, path, overwrite: true);
    }

    /// <summary>
    /// Replaces everything waiting at <paramref name="path"/> with <paramref name="passes"/>. An empty
    /// list leaves no file behind, so nothing reads as pending.
    /// </summary>
    public static void WriteAll(string path, IReadOnlyList<SafeModePass> passes)
    {
        if (passes.Count == 0)
        {
            Remove(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside and moved into place, so a reader never sees half a file: the app and the
        // companion read this while the other may be writing it.
        var beside = path + ".writing";
        File.WriteAllText(beside, JsonSerializer.Serialize(passes, SerializerOptions));
        File.Move(beside, path, overwrite: true);
    }

    /// <summary>
    /// Adds <paramref name="pass"/> to the queue, dropping whatever the same Tweak already had
    /// waiting: the user's latest choice wins, lands at the end, and the others keep their order.
    /// </summary>
    public static void Append(string path, SafeModePass pass)
    {
        var passes = ReadList(path)
            .Where(existing => existing.TweakId != pass.TweakId)
            .ToList();

        passes.Add(pass);

        WriteAll(path, passes);
    }

    /// <summary>
    /// Forgets everything waiting for <paramref name="tweakId"/>, so nothing is left pending on it.
    /// The file goes when it was the last thing in it.
    /// </summary>
    public static void RemoveTweak(string path, string tweakId)
    {
        WriteAll(path, ReadList(path).Where(pass => pass.TweakId != tweakId).ToList());
    }

    /// <summary>Forgets the pass, so nothing is left waiting on it.</summary>
    public static void Remove(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A pass nobody reads is harmless; the next one overwrites it.
        }
    }
}
