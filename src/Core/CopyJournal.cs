using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lazer2Stable.Core;

/// <summary>Un score ajouté à scores.db, de quoi le retrouver pour l'annulation.</summary>
public sealed record JournalScore(string? ReplayMd5, string Key, string ReplayPath);

/// <summary>
/// Ce que la dernière copie a ajouté à stable, enregistré pour pouvoir l'annuler
/// (%LOCALAPPDATA%\lazer2stable\last-copy.json).
/// </summary>
public sealed class CopyJournal
{
    public DateTime Date { get; set; } = DateTime.Now;
    public string? SongsDir { get; set; }
    public string? InstallDir { get; set; }

    /// <summary>Dossiers de maps ou fichiers .osz créés dans Songs.</summary>
    public List<string> Maps { get; set; } = [];

    public List<string> Skins { get; set; } = [];
    public List<JournalScore> Scores { get; set; } = [];
    public List<string> AddedCollections { get; set; } = [];
    public Dictionary<string, List<string>> AddedToCollections { get; set; } = [];

    [JsonIgnore]
    public bool IsEmpty => Maps.Count == 0 && Skins.Count == 0 && Scores.Count == 0 && AddedCollections.Count == 0 && AddedToCollections.Count == 0;

    [JsonIgnore]
    public bool HasCollections => AddedCollections.Count > 0 || AddedToCollections.Count > 0;

    private static string FilePath =>
        Path.Combine(AppPaths.Local, "last-copy.json");

    private static string OldFilePath => Path.Combine(AppPaths.OldLocal, "derniere-copie.json");

    public static CopyJournal? Load()
    {
        try
        {
            string path = File.Exists(FilePath) ? FilePath : OldFilePath;
            if (!File.Exists(path)) return null;
            var j = JsonSerializer.Deserialize<CopyJournal>(File.ReadAllText(path));
            return j is { IsEmpty: false } ? j : null;
        }
        catch
        {
            return null;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // non bloquant : l'annulation ne sera simplement pas proposée
        }
    }

    public static void Delete()
    {
        foreach (string path in new[] { FilePath, OldFilePath })
        {
            try { File.Delete(path); }
            catch { /* rien */ }
        }
    }
}

public sealed class UndoResult
{
    public int Maps { get; set; }
    public int Skins { get; set; }
    public int Scores { get; set; }
    public int Collections { get; set; }
    public int CollectionMaps { get; set; }

    /// <summary>Éléments déjà retirés à la main (introuvables) : rien à faire pour eux.</summary>
    public int AlreadyGone { get; set; }

    public List<string> Errors { get; } = [];
}

/// <summary>Annule la dernière copie : retire de stable exactement ce qu'elle avait ajouté.</summary>
public static class CopyUndo
{
    public static UndoResult Run(CopyJournal journal)
    {
        var result = new UndoResult();

        // 1. Maps : dossier, .osz, ou dossier créé par stable en important le .osz.
        foreach (string path in journal.Maps)
        {
            string? target = Directory.Exists(path) || File.Exists(path) ? path : ImportedFolder(path);
            if (target == null)
            {
                result.AlreadyGone++;
                continue;
            }

            if (Recycle(target, result)) result.Maps++;
        }

        // 2. Skins.
        foreach (string path in journal.Skins)
        {
            if (!Directory.Exists(path))
            {
                result.AlreadyGone++;
                continue;
            }

            if (Recycle(path, result)) result.Skins++;
        }

        // 3. Scores : les entrées ajoutées à scores.db, puis leurs replays.
        if (journal.Scores.Count > 0 && journal.InstallDir != null)
        {
            string dbPath = StableScoresDb.PathIn(journal.InstallDir);
            try
            {
                if (File.Exists(dbPath))
                {
                    var db = StableScoresDb.Read(dbPath);
                    int removed = db.Remove(
                        journal.Scores.Where(s => s.ReplayMd5 != null).Select(s => s.ReplayMd5!).ToList(),
                        journal.Scores.Select(s => s.Key).ToList());

                    if (removed > 0)
                    {
                        File.Copy(dbPath, Path.Combine(journal.InstallDir, $"scores.db.lazer2stable-before-undo-{DateTime.Now:yyyyMMdd-HHmmss}"));
                        string temp = dbPath + ".lazer2stable-tmp";
                        int expected = db.Count;
                        db.Write(temp, []);
                        if (StableScoresDb.Read(temp).Count != expected)
                        {
                            File.Delete(temp);
                            throw new InvalidDataException(Loc.T("ErrScoresVerify"));
                        }

                        File.Move(temp, dbPath, overwrite: true);
                    }

                    result.Scores = removed;
                }

                foreach (var s in journal.Scores)
                {
                    try
                    {
                        if (File.Exists(s.ReplayPath)) File.Delete(s.ReplayPath);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        result.Errors.Add(Loc.F("Line", s.ReplayPath, e.Message));
                    }
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(Loc.F("Line", "scores.db", e.Message));
            }
        }

        // 4. Collections : celles créées, et les maps ajoutées aux autres.
        if (journal.HasCollections && journal.InstallDir != null)
        {
            try
            {
                var (c, h) = StableCollections.Undo(Path.Combine(journal.InstallDir, "collection.db"), journal.AddedCollections, journal.AddedToCollections);
                result.Collections = c;
                result.CollectionMaps = h;
            }
            catch (Exception e)
            {
                result.Errors.Add(Loc.F("Line", "collection.db", e.Message));
            }
        }

        if (result.Errors.Count == 0) CopyJournal.Delete();
        return result;
    }

    /// <summary>stable importe un .osz dans un dossier du même nom, puis supprime le .osz.</summary>
    private static string? ImportedFolder(string path)
    {
        if (!path.EndsWith(".osz", StringComparison.OrdinalIgnoreCase)) return null;
        string folder = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));
        return Directory.Exists(folder) ? folder : null;
    }

    private static bool Recycle(string path, UndoResult result)
    {
        try
        {
            RecycleBin.Send(path);
            return true;
        }
        catch (Exception e)
        {
            result.Errors.Add(Loc.F("Line", path, e.Message));
            return false;
        }
    }
}

/// <summary>Envoie un fichier ou un dossier à la corbeille de Windows (suppression simple ailleurs).</summary>
public static class RecycleBin
{
    public static void Send(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
            return;
        }

        var op = new ShFileOp
        {
            Func = FoDelete,
            From = Path.GetFullPath(path) + "\0",
            Flags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi,
        };

        int code = SHFileOperation(ref op);
        if (code != 0 || op.AnyOperationsAborted)
            throw new IOException(Loc.F("ErrRecycle", code));
        if (Directory.Exists(path) || File.Exists(path))
            throw new IOException(Loc.F("ErrRecycle", "-"));
    }

    private const uint FoDelete = 3;
    private const ushort FofSilent = 0x4, FofNoConfirmation = 0x10, FofAllowUndo = 0x40, FofNoErrorUi = 0x400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOp
    {
        public IntPtr Hwnd;
        public uint Func;
        [MarshalAs(UnmanagedType.LPWStr)] public string From;
        [MarshalAs(UnmanagedType.LPWStr)] public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref ShFileOp op);
}
