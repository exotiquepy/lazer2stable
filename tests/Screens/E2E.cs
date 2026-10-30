using System.Security.Cryptography;
using Avalonia.Threading;
using Exotique.App;
using Exotique.App.ViewModels;
using Exotique.Core;

// Parcours complet piloté par les vraies commandes :
// analyse, sélection, copie (maps, collections, skins, scores), annulation, puis copie des scores seuls.
internal static class E2E
{
    public static void Run(string lazerDir, string stableDir, string? pristineStableDir)
    {
        var saved = Settings.Load();
        saved.CopyBeatmaps = true;
        saved.CopyScores = true;
        saved.CopySkins = true;
        saved.ScoresIncludeOthers = false;
        saved.Save();
        CopyJournal.Delete();

        var vm = new MainViewModel();
        vm.TestUseLocations(Paths.LazerAt(lazerDir), Paths.StableAt(stableDir));
        var w = new MainWindow(vm, autoStart: false) { Width = 1280, Height = 820 };
        w.Show();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Wait(vm.AnalyzeCommand.ExecuteAsync(null));
        Console.WriteLine($"analyse : {sw.ElapsedMilliseconds} ms, à copier {vm.ToCopyCount}, déjà là {vm.PresentCount}, ignorées {vm.IgnoredCount}");
        Console.WriteLine($"étapes : {vm.LazerStep.Detail} | {vm.SongsStep.Detail} | {vm.CompareStep.Detail}");
        Console.WriteLine($"collections : {vm.CollectionsDetail} / dispo={vm.CollectionsAvailable}");
        Console.WriteLine($"scores : à copier {vm.ScoreToCopyCount}, déjà là {vm.ScorePresentCount}, ignorés {vm.ScoreIgnoredCount}");
        Console.WriteLine($"skins : à copier {vm.SkinToCopyCount}, déjà là {vm.SkinPresentCount}, ignorées {vm.SkinIgnoredCount}");
        foreach (var k in vm.TestSkins)
            Console.WriteLine($"  skin {k.Title} -> {(k.IsCopyCandidate ? k.FolderText : k.ReasonText)}");
        Console.WriteLine($"titre : {vm.Headline} ; annulation proposée : {vm.HasLastCopy}");

        var unchecked2 = vm.VisibleItems.Where(i => i.IsCopyCandidate).Take(2).ToList();
        foreach (var i in unchecked2) i.IsSelected = false;
        vm.TestFlushSelection();
        Pump(0.3);
        Console.WriteLine($"bouton : {vm.ExportButtonText} ; sélection : {vm.SelectionText}");

        vm.AskExportCommand.Execute(null);
        Pump(0.2);
        Console.WriteLine($"confirmation : {vm.ConfirmTitle} | {vm.ConfirmDestination.Replace("\n", " + ")}");
        Wait(vm.ConfirmExportCommand.ExecuteAsync(null));
        Console.WriteLine($"bilan : {vm.DoneHeadline} | {vm.DoneScores} | {vm.DoneSkins}");
        Console.WriteLine($"suite : {vm.NextStepText}");
        Console.WriteLine($"Skins de stable : {string.Join(" ; ", Directory.GetDirectories(Path.Combine(stableDir, "Skins")).Select(Path.GetFileName).Order())}");
        Console.WriteLine($"annulation proposée : {vm.HasLastCopy} « {vm.UndoLinkText} »");

        // Annulation : stable doit revenir exactement à son état d'avant.
        vm.AskUndoCommand.Execute(null);
        Pump(0.2);
        Console.WriteLine($"annuler ? {vm.UndoBody}");
        Wait(vm.ConfirmUndoCommand.ExecuteAsync(null));
        Console.WriteLine($"{vm.InfoTitle} : {vm.InfoText.Replace("\n", " ")}");
        vm.DismissInfoCommand.Execute(null);
        if (pristineStableDir != null)
        {
            Console.WriteLine($"  scores.db identique à l'origine : {SameFile(stableDir, pristineStableDir, "scores.db")}");
            Console.WriteLine($"  collection.db identique à l'origine : {SameFile(stableDir, pristineStableDir, "collection.db")}");
            Console.WriteLine($"  Songs : {Count(stableDir, "Songs")} éléments (origine {Count(pristineStableDir, "Songs")})");
            Console.WriteLine($"  Skins : {Count(stableDir, "Skins")} (origine {Count(pristineStableDir, "Skins")})");
            Console.WriteLine($"  Data\\r : {Directory.GetFiles(Path.Combine(stableDir, "Data", "r")).Length} (origine {Directory.GetFiles(Path.Combine(pristineStableDir, "Data", "r")).Length})");
        }

        Console.WriteLine($"après annulation : maps {vm.ToCopyCount}, scores {vm.ScoreToCopyCount}, skins {vm.SkinToCopyCount}, annulation proposée : {vm.HasLastCopy}");

        // Deuxième passage : seulement les scores, avec les replays des autres joueurs.
        vm.IncludeMaps = false;
        vm.IncludeSkins = false;
        vm.IncludeOthers = true;
        vm.TestFlushSelection();
        Pump(0.3);
        Console.WriteLine($"scores seuls : bouton « {vm.ExportButtonText} », sélection : {vm.SelectionText}");
        vm.AskExportCommand.Execute(null);
        Pump(0.2);
        Wait(vm.ConfirmExportCommand.ExecuteAsync(null));
        Console.WriteLine($"bilan : {vm.DoneHeadline} | {vm.DoneScores}");
        Wait(vm.BackToListCommand.ExecuteAsync(null));
        Console.WriteLine($"après : maps {vm.ToCopyCount}, scores à copier {vm.ScoreToCopyCount} (déjà là {vm.ScorePresentCount}), skins {vm.SkinToCopyCount}");

        saved = Settings.Load();
        saved.CopyBeatmaps = true;
        saved.CopySkins = true;
        saved.ScoresIncludeOthers = false;
        saved.Save();
    }

    private static bool SameFile(string a, string b, string name) =>
        File.Exists(Path.Combine(a, name)) && File.Exists(Path.Combine(b, name))
        && SHA256.HashData(File.ReadAllBytes(Path.Combine(a, name))).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(b, name))));

    private static int Count(string root, string sub) =>
        Directory.GetFileSystemEntries(Path.Combine(root, sub)).Count(p => !Path.GetFileName(p).StartsWith('.'));

    private static void Wait(Task t)
    {
        while (!t.IsCompleted) Pump(0.05);
        if (t.IsFaulted) throw t.Exception!;
    }

    private static void Pump(double seconds)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        do
        {
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(10);
        } while (DateTime.UtcNow < end);
        Dispatcher.UIThread.RunJobs();
    }
}
