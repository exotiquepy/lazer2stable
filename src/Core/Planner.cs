namespace Lazer2Stable.Core;

public enum Decision
{
    Copy,
    AlreadyPresent,
    /// <summary>Créée ou modifiée dans l'éditeur lazer (format v128+), illisible par stable telle quelle.</summary>
    LazerEdited,
    /// <summary>Uniquement des modes de jeu custom (rulesets lazer), que stable ne sait pas lire.</summary>
    UnsupportedRuleset,
    /// <summary>Fichiers .osu absents du stockage lazer.</summary>
    MissingFiles,
    /// <summary>Supprimée, protégée (contenu interne de lazer) ou vide : jamais affichée.</summary>
    Skipped,
}

public sealed class PlanItem(LazerSet set, Decision decision, string? reasonKey = null, string? reasonArg = null)
{
    public LazerSet Set { get; } = set;
    public Decision Decision { get; } = decision;
    public string? ReasonKey { get; } = reasonKey;
    public string? ReasonArg { get; } = reasonArg;
    public long Bytes { get; init; }

    /// <summary>Raison traduite dans la langue en cours.</summary>
    public string Reason => ReasonKey == null ? "" : ReasonArg == null ? Loc.T(ReasonKey) : Loc.F(ReasonKey, ReasonArg);
}

public sealed class Plan(List<PlanItem> items)
{
    public List<PlanItem> Items { get; } = items;

    public IEnumerable<PlanItem> With(Decision d) => Items.Where(i => i.Decision == d);

    public int Count(Decision d) => Items.Count(i => i.Decision == d);
}

public static class Planner
{
    // 0 = osu!, 1 = taiko, 2 = catch, 3 = mania
    public static bool IsStableRuleset(int id) => id is >= 0 and <= 3;

    // Version écrite par l'éditeur de lazer (LegacyBeatmapEncoder.FIRST_LAZER_VERSION).
    private const int FirstLazerFormatVersion = 128;

    public static Plan Build(List<LazerSet> sets, StableIndex stable, LazerLocation lazer, IProgress<double>? progress = null)
    {
        var items = new List<PlanItem>(sets.Count);
        int seen = 0;

        // Évite de copier deux fois la même map si lazer l'a en double.
        var plannedSetIds = new HashSet<int>();
        var plannedMd5s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets.OrderBy(s => s.OnlineId <= 0).ThenBy(s => s.OnlineId))
        {
            if (++seen % 200 == 0) progress?.Report((double)seen / sets.Count);

            if (set.DeletePending || set.Protected || set.Beatmaps.Count == 0 || set.Files.Count == 0)
            {
                items.Add(new PlanItem(set, Decision.Skipped));
                continue;
            }

            string? present = PresentReason(set, stable);
            if (present == null && IsPlanned(set, plannedSetIds, plannedMd5s))
                present = "ReasonDuplicate";

            if (present != null)
            {
                items.Add(new PlanItem(set, Decision.AlreadyPresent, present));
                continue;
            }

            if (!set.Beatmaps.Any(b => IsStableRuleset(b.RulesetId)))
            {
                items.Add(new PlanItem(set, Decision.UnsupportedRuleset, "ReasonRuleset"));
                continue;
            }

            var osuFiles = set.Files.Where(f => f.Filename.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)).ToList();
            var missingOsu = osuFiles.Where(f => !File.Exists(lazer.PathForHash(f.Hash))).ToList();
            if (osuFiles.Count == 0 || missingOsu.Count > 0)
            {
                string what = osuFiles.Count == 0 ? Loc.T("ReasonNoOsuFile") : string.Join(", ", missingOsu.Select(f => f.Filename));
                items.Add(new PlanItem(set, Decision.MissingFiles, "ReasonMissingFiles", what));
                continue;
            }

            var edited = osuFiles.Where(f => SafeFormatVersion(lazer.PathForHash(f.Hash)) >= FirstLazerFormatVersion).ToList();
            if (edited.Count > 0)
            {
                items.Add(new PlanItem(set, Decision.LazerEdited, "ReasonEdited"));
                continue;
            }

            long bytes = 0;
            foreach (var f in set.Files)
            {
                try { bytes += new FileInfo(lazer.PathForHash(f.Hash)).Length; }
                catch { /* manquant : signalé à la copie */ }
            }

            items.Add(new PlanItem(set, Decision.Copy) { Bytes = bytes });

            if (set.OnlineId > 0) plannedSetIds.Add(set.OnlineId);
            foreach (var b in set.Beatmaps)
                if (b.Md5.Length > 0) plannedMd5s.Add(b.Md5);
        }

        return new Plan(items);
    }

    /// <summary>Pourquoi stable a déjà cette map (clé de traduction), ou null s'il ne l'a pas.</summary>
    private static string? PresentReason(LazerSet set, StableIndex stable)
    {
        if (set.OnlineId > 0 && stable.SetIds.Contains(set.OnlineId))
            return "ReasonSameSet";

        foreach (var b in set.Beatmaps)
        {
            if (b.Md5.Length > 0 && stable.Md5s.Contains(b.Md5)) return "ReasonSameFile";
            if (b.OnlineId > 0 && stable.BeatmapIds.Contains(b.OnlineId)) return "ReasonSameDiff";
        }

        return null;
    }

    private static bool IsPlanned(LazerSet set, HashSet<int> setIds, HashSet<string> md5s) =>
        (set.OnlineId > 0 && setIds.Contains(set.OnlineId)) || set.Beatmaps.Any(b => b.Md5.Length > 0 && md5s.Contains(b.Md5));

    private static int SafeFormatVersion(string path)
    {
        try { return OsuInfo.ReadFormatVersion(path); }
        catch { return -1; }
    }
}
