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
