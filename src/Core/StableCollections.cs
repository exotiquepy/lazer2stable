using System.Text;

namespace Exotique.Core;

public sealed record StableCollection(string Name, List<string> Md5s);

public sealed record CollectionsExportResult(int Added, int Updated, int HashesAdded, string? BackupPath, string DbPath);

/// <summary>
/// Lecture / écriture du collection.db de stable :
/// Int32 version, Int32 nombre, puis pour chaque collection : String nom, Int32 n, n × String MD5.
/// Une String osu! vaut 0x00 (vide) ou 0x0B + longueur ULEB128 + octets UTF-8.
/// </summary>
public static class StableCollections
{
    // Version écrite si aucun collection.db n'existe encore (format aaaammjj de stable).
    private const int DefaultVersion = 20240820;

    public static (int Version, List<StableCollection> Collections) Read(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
        int version = reader.ReadInt32();
        int count = reader.ReadInt32();
        if (count < 0 || count > 1_000_000) throw new InvalidDataException(Loc.T("ErrCollUnreadable"));

        var list = new List<StableCollection>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadString(reader) ?? "";
            int n = reader.ReadInt32();
            if (n < 0 || n > 10_000_000) throw new InvalidDataException(Loc.T("ErrCollUnreadable"));

            var hashes = new List<string>(n);
            for (int k = 0; k < n; k++)
            {
                string? h = ReadString(reader);
                if (!string.IsNullOrEmpty(h)) hashes.Add(h);
            }

            list.Add(new StableCollection(name, hashes));
        }

        return (version, list);
    }

    public static void Write(string path, int version, IReadOnlyList<StableCollection> collections)
    {
        using var writer = new BinaryWriter(File.Create(path), Encoding.UTF8);
        writer.Write(version);
        writer.Write(collections.Count);
        foreach (var c in collections)
        {
            WriteString(writer, c.Name);
            writer.Write(c.Md5s.Count);
            foreach (string h in c.Md5s) WriteString(writer, h);
        }
    }

    /// <summary>Ce que donnerait la fusion, sans rien écrire (pour l'aperçu avant export).</summary>
    public static (int Added, int Updated, int HashesAdded) Preview(string? dbPath, IReadOnlyList<LazerCollection> lazer)
    {
        var existing = dbPath != null && File.Exists(dbPath) ? Read(dbPath).Collections : [];
        var (_, added, updated, hashes) = Merge(existing, lazer);
        return (added, updated, hashes);
    }

    /// <summary>
    /// Fusionne les collections lazer dans collection.db : une collection de même nom reçoit les maps qui lui manquent,
    /// les autres sont ajoutées. Rien n'est retiré. L'ancien fichier est sauvegardé à côté avant d'être remplacé.
    /// </summary>
    public static CollectionsExportResult Export(string dbPath, IReadOnlyList<LazerCollection> lazer)
    {
        int version = DefaultVersion;
        List<StableCollection> existing = [];
        string? backup = null;

        if (File.Exists(dbPath))
        {
            (version, existing) = Read(dbPath);
            backup = Path.Combine(Path.GetDirectoryName(dbPath)!, $"collection.db.avant-exotique-{DateTime.Now:yyyyMMdd-HHmmss}");
            File.Copy(dbPath, backup);
        }

        var (merged, added, updated, hashesAdded) = Merge(existing, lazer);

        string temp = dbPath + ".tmp";
        Write(temp, version, merged);

        // Relecture de contrôle avant de remplacer l'original.
        var (_, check) = Read(temp);
        if (check.Count != merged.Count)
            throw new InvalidDataException(Loc.T("ErrCollVerify"));

        File.Move(temp, dbPath, overwrite: true);
        return new CollectionsExportResult(added, updated, hashesAdded, backup, dbPath);
    }

    private static (List<StableCollection> Merged, int Added, int Updated, int HashesAdded) Merge(
        IReadOnlyList<StableCollection> existing, IReadOnlyList<LazerCollection> lazer)
    {
        var merged = existing.Select(c => new StableCollection(c.Name, [.. c.Md5s])).ToList();
        int added = 0, updated = 0, hashesAdded = 0;

        foreach (var lc in lazer)
        {
            var target = merged.FirstOrDefault(c => c.Name == lc.Name);
            if (target == null)
            {
                merged.Add(new StableCollection(lc.Name, [.. lc.Md5s]));
                added++;
                hashesAdded += lc.Md5s.Count;
                continue;
            }

            var known = new HashSet<string>(target.Md5s, StringComparer.OrdinalIgnoreCase);
            int before = target.Md5s.Count;
            foreach (string h in lc.Md5s)
                if (known.Add(h))
                    target.Md5s.Add(h);

            if (target.Md5s.Count > before)
            {
                updated++;
                hashesAdded += target.Md5s.Count - before;
            }
        }

        return (merged, added, updated, hashesAdded);
    }

    private static string? ReadString(BinaryReader r)
    {
        byte marker = r.ReadByte();
        if (marker == 0x00) return null;
        if (marker != 0x0B) throw new InvalidDataException(Loc.T("ErrCollUnreadable"));

        int length = 0, shift = 0;
        while (true)
        {
            byte b = r.ReadByte();
            length |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 28) throw new InvalidDataException(Loc.T("ErrCollUnreadable"));
        }

        return Encoding.UTF8.GetString(r.ReadBytes(length));
    }

    private static void WriteString(BinaryWriter w, string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            w.Write((byte)0x00);
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(s);
        w.Write((byte)0x0B);
        uint len = (uint)bytes.Length;
        do
        {
            byte b = (byte)(len & 0x7F);
            len >>= 7;
            if (len != 0) b |= 0x80;
            w.Write(b);
        } while (len != 0);
        w.Write(bytes);
    }
}
