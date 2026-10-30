using System.Globalization;
using System.Text;

namespace Exotique.Core;

/// <summary>Rapport texte de chaque copie, dans la langue de l'interface.</summary>
public static class Report
{
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Exotique", "rapports");

    /// <returns>Chemin du rapport écrit.</returns>
    public static string Write(LazerLocation lazer, StableLocation stable, Plan plan, IReadOnlyCollection<PlanItem> selected,
        ExportResult result, CollectionsExportResult? collections, string? collectionsError,
        ScorePlan? scorePlan = null, ScoreExportResult? scores = null, string? scoresError = null)
    {
        var sb = new StringBuilder();
        string date = DateTime.Now.ToString(Loc.T("RptDate"), CultureInfo.InvariantCulture);
        sb.AppendLine(Loc.F("RptTitle", AppInfo.DisplayVersion, date));
        sb.AppendLine(Loc.F("RptLazer", lazer.StorageDir));
        sb.AppendLine(Loc.F("RptSongs", stable.SongsDir));
        sb.AppendLine();

        // Seulement des scores : pas de bloc « maps » vide.
        if (selected.Count > 0 || scores == null)
        {
            sb.AppendLine(Loc.F("RptSelected", selected.Count));
            sb.AppendLine(Loc.F("RptCopied", result.Exported.Count, Fmt.Bytes(result.BytesCopied)));
            if (result.Failed.Count > 0) sb.AppendLine(Loc.F("RptFailed", result.Failed.Count));
            if (result.Cancelled) sb.AppendLine(Loc.T("RptCancelled"));
            if (result.DiskFull) sb.AppendLine(Loc.T("RptDiskFull"));
            sb.AppendLine();
        }

        if (collections != null)
        {
            sb.AppendLine(Loc.F("RptCollections", collections.Added, collections.Updated, collections.HashesAdded));
            if (collections.BackupPath != null) sb.AppendLine(Loc.F("RptBackup", collections.BackupPath));
            sb.AppendLine();
        }
        else if (collectionsError != null)
        {
            sb.AppendLine(Loc.F("RptCollNot", collectionsError));
            sb.AppendLine();
        }

        if (scores != null && scores.Error == null)
        {
            sb.AppendLine(Loc.F("RptScores", scores.Added.Count, scores.Failed.Count, scores.MissingMap.Count));
            if (scores.BackupPath != null) sb.AppendLine(Loc.F("RptScoresBackup", scores.BackupPath));
            sb.AppendLine();
        }
        else if ((scores?.Error ?? scoresError) is string scoreError)
        {
            sb.AppendLine(Loc.F("RptScoresNot", scoreError));
            sb.AppendLine();
        }

        Section(sb, "RptSecCopied", result.Exported.Select(e => $"{e.Item.Set.DisplayName}  ->  {e.Destination}"));
        Section(sb, "RptSecFailed", result.Failed.Select(f => Loc.F("Line", f.Item.Set.DisplayName, f.Error)));
        Section(sb, "RptSecWarnings", result.Warnings.Select(w => w.ToString()));

        var handled = result.Exported.Select(e => e.Item).Concat(result.Failed.Select(f => f.Item)).ToHashSet();
        Section(sb, "RptSecNotCopied", selected.Where(i => !handled.Contains(i)).Select(i => i.Set.DisplayName));
        if (selected.Count > 0 || scores == null)
            Section(sb, "RptSecUnselected", plan.With(Decision.Copy).Where(i => !selected.Contains(i)).Select(i => i.Set.DisplayName));
        Section(sb, "RptSecEdited", Lines(plan, Decision.LazerEdited));
        Section(sb, "RptSecRuleset", Lines(plan, Decision.UnsupportedRuleset));
        Section(sb, "RptSecMissing", Lines(plan, Decision.MissingFiles));

        if (scores != null)
        {
            Section(sb, "RptSecScoresAdded", scores.Added.Select(i => i.DisplayName));
            Section(sb, "RptSecScoresFailed", scores.Error == null ? scores.Failed.Select(f => Loc.F("Line", f.Item.DisplayName, f.Error)) : []);
            Section(sb, "RptSecScoresNoMap", scores.MissingMap.Select(i => i.DisplayName));
        }

        if (scorePlan != null)
            Section(sb, "RptSecScoresIgnored", scorePlan.Items.Where(i => i.Decision == ScoreDecision.Ignored)
                .Select(i => Loc.F("Line", i.DisplayName, i.Reason)));

        string name = $"{DateTime.Now:yyyy-MM-dd_HHmmss}.txt";
        string path;
        try
        {
            Directory.CreateDirectory(DefaultDirectory);
            path = Path.Combine(DefaultDirectory, "rapport-" + name);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch
        {
            path = Path.Combine(Path.GetTempPath(), "Exotique-rapport-" + name);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        return path;
    }

    private static IEnumerable<string> Lines(Plan plan, Decision d) =>
        plan.With(d).Select(i => Loc.F("Line", i.Set.DisplayName, i.Reason));

    private static void Section(StringBuilder sb, string titleKey, IEnumerable<string> lines)
    {
        var list = lines.ToList();
        if (list.Count == 0) return;

        sb.AppendLine($"== {Loc.T(titleKey)} ({list.Count}) ==");
        foreach (string l in list) sb.AppendLine("  " + l);
        sb.AppendLine();
    }
}
