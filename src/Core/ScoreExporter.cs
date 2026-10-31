namespace Lazer2Stable.Core;

public sealed class ScoreExportResult
{
    public List<ScorePlanItem> Added { get; } = [];
    public List<(ScorePlanItem Item, string Error)> Failed { get; } = [];

    /// <summary>Scores dont la map n'est pas dans stable (elle n'a pas été copiée).</summary>
    public List<ScorePlanItem> MissingMap { get; } = [];

    public string? BackupPath { get; set; }

    /// <summary>De quoi retirer ces scores plus tard (annulation de la copie).</summary>
    public List<JournalScore> Journal { get; } = [];

    /// <summary>Échec de l'écriture de scores.db : aucun score n'a été ajouté.</summary>
    public string? Error { get; set; }
}

/// <param name="Done">Scores traités.</param>
public sealed record ScoreExportProgress(int Done, int Total, ScorePlanItem? Current);

/// <summary>
/// Ajoute des scores lazer au classement Local de stable : le replay va dans Data\r, l'entrée dans scores.db.
/// Les replays joués dans lazer sont réécrits au format de stable (version de stable, score Classic) ;
/// ceux venus de stable sont copiés tels quels.
/// </summary>
public static class ScoreExporter
{
    public static ScoreExportResult Run(IReadOnlyList<ScorePlanItem> items, LazerLocation lazer, string installDir,
        ISet<string> availableMd5s, int stableVersion, IProgress<ScoreExportProgress>? progress = null)
    {
        var result = new ScoreExportResult();
        string dbPath = StableScoresDb.PathIn(installDir);
        string replaysDir = StableScoresDb.ReplaysDirIn(installDir);

        StableScoresDb db;
        try
        {
            // Relu au dernier moment : stable a pu enregistrer un score depuis l'analyse.
            db = File.Exists(dbPath) ? StableScoresDb.Read(dbPath) : StableScoresDb.Empty(stableVersion);
            Directory.CreateDirectory(replaysDir);
        }
        catch (Exception e)
        {
            result.Error = e.Message;
            return result;
        }

        var additions = new List<OsuScore>();
        var written = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            progress?.Report(new ScoreExportProgress(i, items.Count, item));

            if (!availableMd5s.Contains(item.BeatmapMd5))
            {
                result.MissingMap.Add(item);
                continue;
            }

            try
            {
                if (item.Score.ReplayHash == null) throw new FileNotFoundException(Loc.T("ReasonScoreNoReplay"));
                byte[] original = File.ReadAllBytes(lazer.PathForHash(item.Score.ReplayHash));
                var replay = OsuScore.ReadReplay(original);

                // Déjà ajouté entre-temps (ou deux fois dans la sélection) : rien à faire.
                if (db.Contains(replay) || !keys.Add(replay.Key)) continue;

                byte[] bytes;
                OsuScore entry;
                if (replay.IsLazer)
                {
                    entry = replay.Clone();
                    entry.Version = stableVersion;
                    entry.TotalScore = item.StableScore;
                    entry.OnlineId = 0; // score lazer : inconnu des serveurs de stable
                    bytes = entry.ToReplayBytes();
                }
                else
                {
                    entry = replay;
                    bytes = original;
                }

                string target = Path.Combine(replaysDir, entry.ReplayFileName);
                if (File.Exists(target)) continue; // déjà dans Data\r

                string temp = target + ".lazer2stable-tmp";
                File.WriteAllBytes(temp, bytes);
                File.Move(temp, target);
                written.Add(target);
                result.Journal.Add(new JournalScore(entry.ReplayMd5, entry.Key, target));

                entry.ReplayData = null;
                additions.Add(entry);
                result.Added.Add(item);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                result.Failed.Add((item, e.Message));
            }
        }

        progress?.Report(new ScoreExportProgress(items.Count, items.Count, null));
        if (additions.Count == 0) return result;

        try
        {
            if (File.Exists(dbPath))
            {
                result.BackupPath = Path.Combine(installDir, $"scores.db.lazer2stable-backup-{DateTime.Now:yyyyMMdd-HHmmss}");
                File.Copy(dbPath, result.BackupPath);
            }

            string temp = dbPath + ".lazer2stable-tmp";
            db.Write(temp, additions);

            // Relecture de contrôle avant de remplacer l'original.
            var check = StableScoresDb.Read(temp);
            if (check.Count != db.Count + additions.Count || additions.Any(a => !check.Contains(a)))
            {
                File.Delete(temp);
                throw new InvalidDataException(Loc.T("ErrScoresVerify"));
            }

            File.Move(temp, dbPath, overwrite: true);
        }
        catch (Exception e)
        {
            // scores.db inchangé : on retire les replays ajoutés pour ne rien laisser à moitié fait.
            foreach (string f in written)
            {
                try { File.Delete(f); }
                catch { /* tant pis */ }
            }

            result.Error = e.Message;
            foreach (var item in result.Added) result.Failed.Add((item, e.Message));
            result.Added.Clear();
            result.Journal.Clear();
        }

        return result;
    }
}
