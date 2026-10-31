using System.Text;

namespace Lazer2Stable.Core;

/// <summary>
/// Un score au format d'osu! stable. Les mêmes champs forment l'en-tête d'un replay (.osr) et une entrée de scores.db ;
/// seule la fin diffère : les données du replay dans un .osr, la valeur -1 dans scores.db.
/// </summary>
public sealed class OsuScore
{
    /// <summary>Les replays enregistrés par lazer ont une version à partir de 30000000 (stable : aaaammjj).</summary>
    public const int FirstLazerVersion = 30_000_000;

    public const int TargetPracticeMod = 1 << 23;

    /// <summary>new DateTime(1601, 1, 1).Ticks : stable nomme ses replays avec la date au format « temps de fichier » Windows.</summary>
    private const long FileTimeEpochTicks = 504_911_232_000_000_000;

    public byte Mode { get; set; }
    public int Version { get; set; }
    public string? BeatmapMd5 { get; set; }
    public string? Player { get; set; }
    public string? ReplayMd5 { get; set; }
    public ushort Count300 { get; set; }
    public ushort Count100 { get; set; }
    public ushort Count50 { get; set; }
    public ushort CountGeki { get; set; }
    public ushort CountKatu { get; set; }
    public ushort CountMiss { get; set; }
    public int TotalScore { get; set; }
    public ushort MaxCombo { get; set; }
    public bool Perfect { get; set; }
    public int Mods { get; set; }
    public string? LifeBar { get; set; }

    /// <summary>Date du score, en ticks UTC.</summary>
    public long Ticks { get; set; }

    /// <summary>Mouvements du replay, compressés (LZMA). Seulement dans un .osr.</summary>
    public byte[]? ReplayData { get; set; }

    public long OnlineId { get; set; }

    /// <summary>Précision cumulée, seulement avec le mod Target Practice.</summary>
    public double? TargetPracticeTotal { get; set; }

    public bool IsLazer => Version >= FirstLazerVersion;

    public DateTime Date => new(Math.Clamp(Ticks, 0, DateTime.MaxValue.Ticks), DateTimeKind.Utc);

    /// <summary>Nom du replay dans Data\r, comme stable le fait : « md5 de la map-date.osr ».</summary>
    public string ReplayFileName => $"{BeatmapMd5}-{Ticks - FileTimeEpochTicks}.osr";

    /// <summary>Clé qui identifie un score même si son replay a été réécrit : map + date.</summary>
    public string Key => $"{BeatmapMd5?.ToLowerInvariant()}|{Ticks}";

    public OsuScore Clone() => (OsuScore)MemberwiseClone();

    // =================================================================== lecture

    /// <summary>Lit les champs communs, jusqu'à la date incluse.</summary>
    /// <param name="strict">Exige un MD5 de map valide (replays) ; scores.db est lu tel quel.</param>
    public static OsuScore ReadHeader(BinaryReader r, bool strict = true)
    {
        var s = new OsuScore
        {
            Mode = r.ReadByte(),
            Version = r.ReadInt32(),
            BeatmapMd5 = OsuBinary.ReadString(r),
            Player = OsuBinary.ReadString(r),
            ReplayMd5 = OsuBinary.ReadString(r),
            Count300 = r.ReadUInt16(),
            Count100 = r.ReadUInt16(),
            Count50 = r.ReadUInt16(),
            CountGeki = r.ReadUInt16(),
            CountKatu = r.ReadUInt16(),
            CountMiss = r.ReadUInt16(),
            TotalScore = r.ReadInt32(),
            MaxCombo = r.ReadUInt16(),
            Perfect = r.ReadBoolean(),
            Mods = r.ReadInt32(),
            LifeBar = OsuBinary.ReadString(r),
            Ticks = r.ReadInt64(),
        };

        if (s.Mode > 3 || (strict && s.BeatmapMd5 is not { Length: 32 }))
            throw new InvalidDataException("not an osu! score");

        return s;
    }

    /// <summary>Lit un .osr complet. Les données ajoutées par lazer à la fin sont ignorées.</summary>
    public static OsuScore ReadReplay(byte[] content)
    {
        using var r = new BinaryReader(new MemoryStream(content, writable: false), Encoding.UTF8);
        var s = ReadHeader(r);

        int length = r.ReadInt32();
        if (length < 0 || length > content.Length) throw new InvalidDataException("bad replay length");
        s.ReplayData = r.ReadBytes(length);
        if (s.ReplayData.Length != length) throw new EndOfStreamException();

        if (s.Version >= 20140721) s.OnlineId = r.ReadInt64();
        else if (s.Version >= 20121008) s.OnlineId = r.ReadInt32();

        if ((s.Mods & TargetPracticeMod) != 0 && r.BaseStream.Position + 8 <= r.BaseStream.Length)
            s.TargetPracticeTotal = r.ReadDouble();

        return s;
    }

    /// <summary>Lit seulement l'en-tête d'un .osr (rapide : quelques centaines d'octets).</summary>
    public static OsuScore ReadReplayHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096);
        using var r = new BinaryReader(stream, Encoding.UTF8);
        return ReadHeader(r);
    }

    /// <summary>Lit une entrée de scores.db.</summary>
    public static OsuScore ReadDbEntry(BinaryReader r)
    {
        var s = ReadHeader(r, strict: false);
        r.ReadInt32(); // toujours -1
        s.OnlineId = r.ReadInt64();
        if ((s.Mods & TargetPracticeMod) != 0) s.TargetPracticeTotal = r.ReadDouble();
        return s;
    }

    // ================================================================= écriture

    private void WriteHeader(BinaryWriter w)
    {
        w.Write(Mode);
        w.Write(Version);
        OsuBinary.WriteString(w, BeatmapMd5);
        OsuBinary.WriteString(w, Player);
        OsuBinary.WriteString(w, ReplayMd5);
        w.Write(Count300);
        w.Write(Count100);
        w.Write(Count50);
        w.Write(CountGeki);
        w.Write(CountKatu);
        w.Write(CountMiss);
        w.Write(TotalScore);
        w.Write(MaxCombo);
        w.Write(Perfect);
        w.Write(Mods);
        OsuBinary.WriteString(w, LifeBar);
        w.Write(Ticks);
    }

    /// <summary>Écrit un .osr que stable sait lire.</summary>
    public byte[] ToReplayBytes()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            WriteHeader(w);
            byte[] data = ReplayData ?? [];
            w.Write(data.Length);
            w.Write(data);
            w.Write(OnlineId);
            if ((Mods & TargetPracticeMod) != 0) w.Write(TargetPracticeTotal ?? 0);
        }

        return ms.ToArray();
    }

    /// <summary>Écrit l'entrée de scores.db correspondante.</summary>
    public void WriteDbEntry(BinaryWriter w)
    {
        WriteHeader(w);
        w.Write(-1);
        w.Write(OnlineId);
        if ((Mods & TargetPracticeMod) != 0) w.Write(TargetPracticeTotal ?? 0);
    }
}

/// <summary>Chaînes au format osu! : 0x00 (absente) ou 0x0B + longueur ULEB128 + octets UTF-8.</summary>
public static class OsuBinary
{
    public static string? ReadString(BinaryReader r)
    {
        byte marker = r.ReadByte();
        if (marker == 0x00) return null;
        if (marker != 0x0B) throw new InvalidDataException("bad osu! string");

        int length = 0, shift = 0;
        while (true)
        {
            byte b = r.ReadByte();
            length |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 28) throw new InvalidDataException("bad osu! string length");
        }

        if (length > 1 << 24) throw new InvalidDataException("osu! string too long");
        byte[] bytes = r.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException();
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>null s'écrit 0x00 ; une chaîne vide s'écrit 0x0B 0x00, comme le font osu! stable et lazer.</summary>
    public static void WriteString(BinaryWriter w, string? s)
    {
        if (s == null)
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
