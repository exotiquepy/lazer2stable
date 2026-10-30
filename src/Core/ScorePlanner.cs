namespace Exotique.Core;

public enum ScoreDecision
{
    Copy,
    AlreadyPresent,
    Ignored,
}

public sealed class ScorePlanItem
{
    public required LazerScore Score { get; init; }

    /// <summary>En-tête du replay stocké par lazer (null s'il est introuvable).</summary>
    public OsuScore? Replay { get; init; }

    public required ScoreDecision Decision { get; init; }
    public string? ReasonKey { get; init; }
    public string? ReasonArg { get; init; }

    public LazerSet? Set { get; init; }
    public LazerBeatmap? Beatmap { get; init; }

    /// <summary>La map n'est pas encore dans stable : elle fait partie des maps à copier, le score la suit.</summary>
    public PlanItem? NeedsMap { get; init; }

    /// <summary>Score de l'utilisateur (son pseudo lazer ou stable, ou joué en invité), et pas un replay téléchargé.</summary>
    public bool IsMine { get; init; }

    /// <summary>Score tel qu'il apparaîtra dans stable.</summary>
    public int StableScore { get; init; }

    public string BeatmapMd5 => Replay?.BeatmapMd5?.ToLowerInvariant() ?? Score.BeatmapMd5;

    public string Player => Replay?.Player ?? Score.Username;

    /// <summary>Joué dans lazer (et pas importé de stable).</summary>
    public bool PlayedInLazer => Replay?.IsLazer ?? !Score.IsLegacy;

    public string Reason => ReasonKey == null ? "" : ReasonArg == null ? Loc.T(ReasonKey) : Loc.F(ReasonKey, ReasonArg);

    /// <summary>« Artiste - Titre [Difficulté] (joueur, score) », pour le rapport.</summary>
    public string DisplayName
    {
        get
        {
            string map = Set == null ? BeatmapMd5 : $"{Set.Artist} - {Set.Title}".Trim(' ', '-');
            if (Beatmap != null && Beatmap.DifficultyName.Length > 0) map += $" [{Beatmap.DifficultyName}]";
            return $"{map} ({Player}, {Fmt.N(StableScore)}, {Score.Date.ToLocalTime():yyyy-MM-dd HH:mm})";
        }
    }
}

public sealed class ScorePlan
{
    public List<ScorePlanItem> Items { get; init; } = [];

    /// <summary>Pourquoi les scores ne peuvent pas être copiés du tout (dossier de stable inconnu, scores.db illisible).</summary>
    public string? Unavailable { get; init; }

    /// <summary>Version de stable écrite dans les replays convertis.</summary>
    public int StableVersion { get; init; } = StableScoresDb.FallbackVersion;

    /// <summary>Pseudos considérés comme ceux de l'utilisateur.</summary>
    public IReadOnlyList<string> PlayerNames { get; init; } = [];

    public int Count(ScoreDecision d) => Items.Count(i => i.Decision == d);
}

/// <summary>Compare les scores de lazer au classement Local de stable (scores.db et Data\r).</summary>
public static class ScorePlanner
{
    public static ScorePlan Build(IReadOnlyList<LazerScore> scores, IReadOnlyList<LazerSet> sets, LazerLocation lazer,
        StableLocation stable, StableIndex index, Plan maps, IProgress<double>? progress = null)
    {
        if (stable.InstallDir == null)
            return new ScorePlan { Unavailable = Loc.T("ScoresNoInstall") };

        StableScoresDb db;
        string dbPath = StableScoresDb.PathIn(stable.InstallDir);
        try
        {
            db = File.Exists(dbPath) ? StableScoresDb.Read(dbPath) : StableScoresDb.Empty(0);
        }
        catch (Exception e)
        {
            return new ScorePlan { Unavailable = Loc.F("ScoresUnreadable", e.Message) };
        }

        var replayFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string replaysDir = StableScoresDb.ReplaysDirIn(stable.InstallDir);
        try
        {
            if (Directory.Exists(replaysDir))
                foreach (string f in Directory.EnumerateFiles(replaysDir, "*.osr"))
                    replayFiles.Add(Path.GetFileName(f));
        }
        catch
        {
            // Data\r illisible : on se fie à scores.db
        }

        var names = PlayerNames(lazer, stable);

        // Map de chaque score : la version non supprimée d'abord.
        var byMd5 = new Dictionary<string, (LazerSet Set, LazerBeatmap Beatmap)>(StringComparer.OrdinalIgnoreCase);
        foreach (var set in sets.OrderBy(s => s.DeletePending))
            foreach (var b in set.Beatmaps)
                if (b.Md5.Length > 0) byMd5.TryAdd(b.Md5, (set, b));

        // Maps qui vont être copiées : leurs scores peuvent suivre.
        var copied = new Dictionary<string, PlanItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in maps.With(Decision.Copy))
            foreach (var b in item.Set.Beatmaps)
                if (b.Md5.Length > 0) copied.TryAdd(b.Md5, item);

        var items = new List<ScorePlanItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int done = 0;

        foreach (var score in scores.OrderByDescending(s => s.Date))
        {
            if (++done % 50 == 0) progress?.Report((double)done / scores.Count);

            byMd5.TryGetValue(score.BeatmapMd5, out var map);

            OsuScore? replay = null;
            if (score.ReplayHash != null)
            {
                try
                {
                    replay = OsuScore.ReadReplayHeader(lazer.PathForHash(score.ReplayHash));
                }
                catch
                {
                    // replay absent ou abîmé
                }
            }

            ScorePlanItem Make(ScoreDecision d, string? key = null, string? arg = null, PlanItem? needs = null) => new()
            {
                Score = score,
                Replay = replay,
                Decision = d,
                ReasonKey = key,
                ReasonArg = arg,
                Set = map.Set,
                Beatmap = map.Beatmap,
                NeedsMap = needs,
                IsMine = IsMine(replay?.Player ?? score.Username, names),
                StableScore = StableScoreOf(score, replay),
            };

            // Déjà dans scores.db (map + date identiques), même si lazer n'a plus le replay.
            string realmKey = $"{score.BeatmapMd5}|{score.Date.UtcTicks}";
            if (db.Keys.Contains(realmKey))
            {
                if (seen.Add(realmKey)) items.Add(Make(ScoreDecision.AlreadyPresent, "ReasonScoreInStable"));
                continue;
            }

            if (score.RulesetId is < 0 or > 3)
            {
                items.Add(Make(ScoreDecision.Ignored, "ReasonScoreRuleset"));
                continue;
            }

            if (replay == null)
            {
                items.Add(Make(ScoreDecision.Ignored, "ReasonScoreNoReplay"));
                continue;
            }

            if (!seen.Add(replay.Key)) continue; // le même score deux fois dans lazer

            if (db.Contains(replay) || replayFiles.Contains(replay.ReplayFileName))
            {
                items.Add(Make(ScoreDecision.AlreadyPresent, "ReasonScoreInStable"));
                continue;
            }

            if (score.UnsupportedMods != null)
            {
                items.Add(Make(ScoreDecision.Ignored, "ReasonScoreMods", score.UnsupportedMods));
                continue;
            }

            if (score.TunedMods != null)
            {
                items.Add(Make(ScoreDecision.Ignored, "ReasonScoreModSettings", score.TunedMods));
                continue;
            }

            string md5 = replay.BeatmapMd5!.ToLowerInvariant();
            if (index.Md5s.Contains(md5))
                items.Add(Make(ScoreDecision.Copy));
            else if (copied.TryGetValue(md5, out var mapItem))
                items.Add(Make(ScoreDecision.Copy, needs: mapItem));
            else if (score.BeatmapOnlineId > 0 && index.BeatmapIds.Contains(score.BeatmapOnlineId))
                items.Add(Make(ScoreDecision.Ignored, "ReasonScoreMapOther"));
            else
                items.Add(Make(ScoreDecision.Ignored, "ReasonScoreMapMissing"));
        }

        progress?.Report(1);

        return new ScorePlan
        {
            Items = items,
            StableVersion = StableScoresDb.GuessVersion(stable.InstallDir, db.Version == 0 ? null : db),
            PlayerNames = names.ToList(),
        };
    }

    /// <summary>Score que stable affichera : celui d'origine pour un score de stable, sinon le score Classic de lazer.</summary>
    public static int StableScoreOf(LazerScore score, OsuScore? replay)
    {
        if (replay is { IsLazer: false }) return replay.TotalScore;
        if (replay == null && score.IsLegacy) return (int)Math.Clamp(score.TotalScore, 0, int.MaxValue);

        long classic = ScoreMath.ClassicFromStandardised(score.RulesetId, score.TotalScore, score.MaxBasicJudgements);
        return (int)Math.Clamp(classic, 0, int.MaxValue);
    }

    private static bool IsMine(string player, HashSet<string> names) =>
        names.Count == 0 || player.Length == 0 || player.Equals("Guest", StringComparison.OrdinalIgnoreCase) || names.Contains(player);

    /// <summary>Pseudos connectés dans lazer (game.ini) et dans stable (osu!.*.cfg).</summary>
    public static HashSet<string> PlayerNames(LazerLocation lazer, StableLocation stable)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lazerConfigs = new List<string> { Path.Combine(lazer.StorageDir, "game.ini") };
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (appData.Length > 0) lazerConfigs.Add(Path.Combine(appData, "osu", "game.ini"));
        foreach (string ini in lazerConfigs.Distinct()) AddUsername(ini, names);

        if (stable.InstallDir != null)
        {
            try
            {
                foreach (string cfg in Directory.EnumerateFiles(stable.InstallDir, "osu!.*.cfg"))
                    AddUsername(cfg, names);
            }
            catch
            {
                // pas grave
            }
        }

        return names;
    }

    private static void AddUsername(string path, HashSet<string> names)
    {
        try
        {
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadLines(path))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0 || !line[..eq].Trim().Equals("Username", StringComparison.OrdinalIgnoreCase)) continue;
                string name = line[(eq + 1)..].Trim();
                if (name.Length > 0) names.Add(name);
            }
        }
        catch
        {
            // fichier illisible
        }
    }
}
