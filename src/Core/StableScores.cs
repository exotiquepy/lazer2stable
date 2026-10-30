using System.Text;

namespace Exotique.Core;

/// <summary>
/// scores.db de stable (classement Local) : Int32 version, Int32 nombre de maps, puis pour chaque map :
/// String MD5, Int32 nombre de scores, et les scores (voir <see cref="OsuScore"/>).
/// Les scores existants sont gardés octet pour octet : Exotique ajoute, il ne réécrit jamais ce qui est déjà là.
/// </summary>
public sealed class StableScoresDb
{
    /// <summary>Version écrite quand on ne trouve aucune version de stable (format aaaammjj).</summary>
    public const int FallbackVersion = 20250107;

    private sealed class Block(string? md5)
    {
        public string? Md5 { get; } = md5;
        public List<byte[]> Raw { get; } = [];
    }

    private readonly List<Block> blocks = [];

    public int Version { get; private set; }

    public HashSet<string> ReplayMd5s { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);

    public int Count { get; private set; }

    public bool Contains(OsuScore s) =>
        (s.ReplayMd5 != null && ReplayMd5s.Contains(s.ReplayMd5)) || Keys.Contains(s.Key);

    public static string PathIn(string installDir) => Path.Combine(installDir, "scores.db");

    public static string ReplaysDirIn(string installDir) => Path.Combine(installDir, "Data", "r");

    public static StableScoresDb Empty(int version) => new() { Version = version };

    public static StableScoresDb Read(string path)
    {
        byte[] content = File.ReadAllBytes(path);
        var db = new StableScoresDb();

        try
        {
            using var ms = new MemoryStream(content, writable: false);
            using var r = new BinaryReader(ms, Encoding.UTF8);
            db.Version = r.ReadInt32();
            int maps = r.ReadInt32();
            if (maps < 0 || maps > 5_000_000) throw new InvalidDataException();

            for (int i = 0; i < maps; i++)
            {
                var block = new Block(OsuBinary.ReadString(r));
                int n = r.ReadInt32();
                if (n < 0 || n > 1_000_000) throw new InvalidDataException();

                for (int k = 0; k < n; k++)
                {
                    long start = ms.Position;
                    var s = OsuScore.ReadDbEntry(r);
                    block.Raw.Add(content[(int)start..(int)ms.Position]);
                    db.Remember(s);
                }

                db.blocks.Add(block);
            }
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException or ArgumentException)
        {
            throw new InvalidDataException(Loc.T("ErrScoresUnreadable"), e);
        }

        return db;
    }

    private void Remember(OsuScore s)
    {
        Count++;
        if (!string.IsNullOrEmpty(s.ReplayMd5)) ReplayMd5s.Add(s.ReplayMd5);
        Keys.Add(s.Key);
    }

    /// <summary>Écrit le fichier avec les scores existants (inchangés) suivis des nouveaux.</summary>
    public void Write(string path, IReadOnlyList<OsuScore> additions)
    {
        var pending = additions
            .GroupBy(a => a.BeatmapMd5 ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var newBlocks = pending.Keys
            .Where(md5 => !blocks.Any(b => string.Equals(b.Md5 ?? "", md5, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        using var w = new BinaryWriter(File.Create(path), Encoding.UTF8);
        w.Write(Version);
        w.Write(blocks.Count + newBlocks.Count);

        foreach (var block in blocks)
        {
            // Une map présente deux fois dans le fichier : les ajouts vont dans le premier bloc.
            var adds = pending.Remove(block.Md5 ?? "", out var list) ? list : [];
            OsuBinary.WriteString(w, block.Md5);
            w.Write(block.Raw.Count + adds.Count);
            foreach (byte[] raw in block.Raw) w.Write(raw);
            foreach (var s in adds) s.WriteDbEntry(w);
        }

        foreach (string md5 in newBlocks)
        {
            var adds = pending[md5];
            OsuBinary.WriteString(w, adds[0].BeatmapMd5);
            w.Write(adds.Count);
            foreach (var s in adds) s.WriteDbEntry(w);
        }
    }

    /// <summary>
    /// Version de stable à écrire dans les replays convertis : celle de scores.db, sinon celle d'osu!.db.
    /// </summary>
    public static int GuessVersion(string installDir, StableScoresDb? db)
    {
        if (db != null && IsStableVersion(db.Version)) return db.Version;

        try
        {
            string osuDb = Path.Combine(installDir, "osu!.db");
            if (File.Exists(osuDb))
            {
                using var r = new BinaryReader(new FileStream(osuDb, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                int v = r.ReadInt32();
                if (IsStableVersion(v)) return v;
            }
        }
        catch
        {
            // pas grave : version par défaut
        }

        return FallbackVersion;
    }

    private static bool IsStableVersion(int v) => v is >= 20_000_000 and < OsuScore.FirstLazerVersion;
}
