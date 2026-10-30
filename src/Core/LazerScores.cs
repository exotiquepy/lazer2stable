using System.Text.Json;
using Realms;

namespace Exotique.Core;

/// <summary>Un score de la base lazer, avec ce qu'il faut pour le convertir et l'afficher.</summary>
public sealed record LazerScore(
    Guid Id,
    string BeatmapMd5,
    int BeatmapOnlineId,
    int RulesetId,
    string Username,
    long TotalScore,
    bool IsLegacy,
    DateTimeOffset Date,
    IReadOnlyList<string> Mods,
    string? UnsupportedMods,
    string? TunedMods,
    int MaxBasicJudgements,
    string? ReplayHash);

/// <summary>Lecture des scores dans la base Realm de lazer (mode dynamique).</summary>
internal static class LazerScoreReader
{
    /// <summary>Mods de lazer qui existent aussi dans stable (acronymes de lazer).</summary>
    private static readonly HashSet<string> StableMods = new(StringComparer.OrdinalIgnoreCase)
    {
        "NF", "EZ", "TD", "HD", "HR", "SD", "DT", "RX", "HT", "NC", "FL", "AT", "SO", "AP", "PF", "FI", "RD", "CN", "TP",
        "MR", "CL", "SV2", "DS", "1K", "2K", "3K", "4K", "5K", "6K", "7K", "8K", "9K",
    };

    // Résultats « de base » de lazer : ceux qui comptent pour le score Classic.
    private static readonly string[] BasicResults = ["miss", "meh", "ok", "good", "great", "perfect"];

    public static List<LazerScore> Read(Realm realm)
    {
        var result = new List<LazerScore>();
        IQueryable<IRealmObject> all;
        try
        {
            all = realm.DynamicApi.All("Score");
        }
        catch
        {
            return result; // pas de scores dans cette base
        }

        foreach (var s in all)
        {
            try
            {
                if (Get<bool>(s, "DeletePending")) continue;

                var beatmap = Get<IRealmObject?>(s, "BeatmapInfo");
                if (beatmap == null) continue; // map supprimée de lazer

                string md5 = (Get<string?>(beatmap, "MD5Hash") ?? "").ToLowerInvariant();
                if (md5.Length == 0) continue;

                var ruleset = Get<IRealmObject?>(s, "Ruleset");
                var user = Get<IEmbeddedObject?>(s, "User");

                string? replay = null;
                foreach (var usage in s.DynamicApi.GetList<IEmbeddedObject>("Files"))
                {
                    string? name = Get<string?>(usage, "Filename");
                    var file = Get<IRealmObject?>(usage, "File");
                    if (file == null || name == null || !name.EndsWith(".osr", StringComparison.OrdinalIgnoreCase)) continue;
                    replay = Get<string?>(file, "Hash")?.ToLowerInvariant();
                }

                var (mods, unsupported, tuned) = ParseMods(Get<string?>(s, "Mods"));

                result.Add(new LazerScore(
                    Id: Get<Guid>(s, "ID"),
                    BeatmapMd5: md5,
                    BeatmapOnlineId: (int)Get<long>(beatmap, "OnlineID", -1),
                    RulesetId: ruleset == null ? -1 : (int)Get<long>(ruleset, "OnlineID", -1),
                    Username: user == null ? "" : Get<string?>(user, "Username") ?? "",
                    TotalScore: Get<long>(s, "TotalScore"),
                    IsLegacy: Get<bool>(s, "IsLegacyScore"),
                    Date: Get<DateTimeOffset>(s, "Date"),
                    Mods: mods,
                    UnsupportedMods: unsupported,
                    TunedMods: tuned,
                    MaxBasicJudgements: CountBasic(Get<string?>(s, "MaximumStatistics")),
                    ReplayHash: replay));
            }
            catch
            {
                // score illisible : on passe au suivant
            }
        }

        return result;
    }

    /// <summary>
    /// Acronymes des mods ; ceux que stable n'a pas ; ceux réglés autrement que par défaut (DT à 1,3x…),
    /// que stable ne saurait pas rejouer.
    /// </summary>
    internal static (List<string> Mods, string? Unsupported, string? Tuned) ParseMods(string? json)
    {
        var mods = new List<string>();
        var unsupported = new List<string>();
        var tuned = new List<string>();
        if (string.IsNullOrWhiteSpace(json)) return (mods, null, null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return (mods, null, null);

            foreach (var m in doc.RootElement.EnumerateArray())
            {
                if (!m.TryGetProperty("acronym", out var a) || a.GetString() is not { Length: > 0 } acronym) continue;
                mods.Add(acronym);
                if (!StableMods.Contains(acronym)) unsupported.Add(acronym);
                else if (m.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object
                         && settings.EnumerateObject().Any())
                    tuned.Add(acronym);
            }
        }
        catch (JsonException)
        {
            // mods illisibles : traités comme absents
        }

        return (mods, unsupported.Count > 0 ? string.Join(" ", unsupported) : null, tuned.Count > 0 ? string.Join(" ", tuned) : null);
    }

    internal static int CountBasic(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            int n = 0;
            foreach (string key in BasicResults)
                if (doc.RootElement.TryGetProperty(key, out var v) && v.TryGetInt32(out int c))
                    n += c;
            return n;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static T Get<T>(IRealmObjectBase o, string prop, T fallback = default!)
    {
        try { return o.DynamicApi.Get<T>(prop); }
        catch { return fallback; }
    }
}

/// <summary>Calculs de stable : précision, rang, et score « Classic » de lazer.</summary>
public static class ScoreMath
{
    private const int HiddenMods = 8 | 1024 | (1 << 20); // HD, FL, FadeIn

    /// <summary>Score affiché par lazer en mode « Classic » (même formule que lazer, ScoreInfoExtensions).</summary>
    public static long ClassicFromStandardised(int rulesetId, long standardised, int basicJudgements)
    {
        const double max = 1_000_000;
        return rulesetId switch
        {
            0 => (long)Math.Round((Math.Pow(basicJudgements, 2) * 32.57 + 100000) * standardised / max),
            1 => (long)Math.Round((basicJudgements * 1109 + 100000) * standardised / max),
            2 => (long)Math.Round(Math.Pow(standardised / max * basicJudgements, 2) * 21.62 + standardised / 10d),
            _ => standardised,
        };
    }

    /// <summary>Précision telle que stable la calcule, de 0 à 1.</summary>
    public static double Accuracy(OsuScore s)
    {
        double n300 = s.Count300, n100 = s.Count100, n50 = s.Count50, geki = s.CountGeki, katu = s.CountKatu, miss = s.CountMiss;
        double total;
        switch (s.Mode)
        {
            case 1:
                total = n300 + n100 + miss;
                return total == 0 ? 0 : (n300 + n100 / 2) / total;
            case 2:
                total = n300 + n100 + n50 + katu + miss;
                return total == 0 ? 0 : (n300 + n100 + n50) / total;
            case 3:
                total = n300 + geki + katu + n100 + n50 + miss;
                return total == 0 ? 0 : (300 * (n300 + geki) + 200 * katu + 100 * n100 + 50 * n50) / (300 * total);
            default:
                total = n300 + n100 + n50 + miss;
                return total == 0 ? 0 : (300 * n300 + 100 * n100 + 50 * n50) / (300 * total);
        }
    }

    /// <summary>Rang affiché par stable : « SS », « S », « A »… ; « SSH » / « SH » en argenté (HD, FL).</summary>
    public static string Grade(OsuScore s)
    {
        string g = BaseGrade(s);
        if ((s.Mods & HiddenMods) != 0 && g is "SS" or "S") g += "H";
        return g;
    }

    private static string BaseGrade(OsuScore s)
    {
        double acc = Accuracy(s);
        switch (s.Mode)
        {
            case 2:
                return acc >= 1 ? "SS" : acc > 0.98 ? "S" : acc > 0.94 ? "A" : acc > 0.90 ? "B" : acc > 0.85 ? "C" : "D";
            case 3:
                return acc >= 1 ? "SS" : acc > 0.95 ? "S" : acc > 0.90 ? "A" : acc > 0.80 ? "B" : acc > 0.70 ? "C" : "D";
            default:
            {
                double total = s.Count300 + s.Count100 + s.Count50 + s.CountMiss;
                if (total == 0) return "D";
                double r300 = s.Count300 / total, r50 = s.Count50 / total;
                bool noMiss = s.CountMiss == 0;
                if (r300 >= 1) return "SS";
                if (r300 > 0.9 && r50 <= 0.01 && noMiss) return "S";
                if ((r300 > 0.8 && noMiss) || r300 > 0.9) return "A";
                if ((r300 > 0.7 && noMiss) || r300 > 0.8) return "B";
                return r300 > 0.6 ? "C" : "D";
            }
        }
    }

    /// <summary>Mods de stable sous forme d'acronymes (pour l'affichage).</summary>
    public static IReadOnlyList<string> ModAcronyms(int mods)
    {
        var list = new List<string>();
        void Add(int bit, string name)
        {
            if ((mods & bit) != 0) list.Add(name);
        }

        Add(1, "NF"); Add(2, "EZ"); Add(4, "TD"); Add(8, "HD"); Add(16, "HR");
        if ((mods & 32) != 0 && (mods & 16384) == 0) list.Add("SD");
        if ((mods & 512) != 0) list.Add("NC"); else Add(64, "DT");
        Add(128, "RX"); Add(256, "HT"); Add(1024, "FL"); Add(4096, "SO"); Add(8192, "AP"); Add(16384, "PF");
        Add(1 << 20, "FI"); Add(1 << 23, "TP"); Add(1 << 29, "SV2"); Add(1 << 30, "MR");
        return list;
    }
}
