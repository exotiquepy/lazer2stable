using Avalonia.Threading;
using Exotique.App;
using Exotique.App.ViewModels;
using Exotique.Core;

// Parcours complet piloté par les vraies commandes : analyse, décocher 2 maps, copier, relancer l'analyse.
internal static class E2E
{
    public static void Run(string lazerDir, string stableDir)
    {
        var vm = new MainViewModel();
        vm.TestUseLocations(Paths.LazerAt(lazerDir), Paths.StableAt(stableDir));
        var w = new MainWindow(vm, autoStart: false) { Width = 1280, Height = 820 };
        w.Show();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Wait(vm.AnalyzeCommand.ExecuteAsync(null));
        Console.WriteLine($"analyse : {sw.ElapsedMilliseconds} ms, à copier {vm.ToCopyCount}, déjà là {vm.PresentCount}, ignorées {vm.IgnoredCount}");
        Console.WriteLine($"progression : {vm.AnalysisProgress:P0} ; étapes : {vm.LazerStep.State} ({vm.LazerStep.Detail}), {vm.SongsStep.State} ({vm.SongsStep.Detail}), {vm.CompareStep.State} ({vm.CompareStep.Detail})");
        Console.WriteLine($"collections : {vm.CollectionsLabel} / {vm.CollectionsDetail} / dispo={vm.CollectionsAvailable}");

        sw.Restart();
        vm.SearchText = "bulk title 12";
        Pump(0.5);
        Console.WriteLine($"recherche « bulk title 12 » : {vm.VisibleItems.Count} résultats en {sw.ElapsedMilliseconds} ms (attente incluse)");
        vm.SearchText = "";
        Pump(0.4);

        // Décocher les deux premières, puis tester Tout décocher / Tout cocher sur un filtre.
        var unchecked2 = vm.VisibleItems.Where(i => i.IsCopyCandidate).Take(2).ToList();
        foreach (var i in unchecked2) i.IsSelected = false;
        vm.ShowOsu = false;
        Pump(0.3);
        int maniaEtc = vm.VisibleItems.Count;
        vm.SelectNoneVisibleCommand.Execute(null);
        Pump(0.3);
        Console.WriteLine($"filtre sans osu! : {maniaEtc} maps ; après « Tout décocher » : {vm.SelectedCount} cochées");
        vm.SelectAllVisibleCommand.Execute(null);
        vm.ShowOsu = true;
        Pump(0.3);
        Console.WriteLine($"après « Tout cocher » : {vm.SelectedCount} cochées, bouton : {vm.ExportButtonText}");

        vm.AskExportCommand.Execute(null);
        Pump(0.2);
        Console.WriteLine($"confirmation ouverte={vm.IsConfirmOpen} : {vm.ConfirmTitle} | {vm.ConfirmBody} | {vm.ConfirmCollections}");

        sw.Restart();
        Wait(vm.ConfirmExportCommand.ExecuteAsync(null));
        Console.WriteLine($"copie : {sw.ElapsedMilliseconds} ms, écran={vm.CurrentScreen}");
        Console.WriteLine($"bilan : {vm.DoneHeadline} | {vm.DoneDetail} | {vm.DoneCollections}");
        Console.WriteLine($"problèmes : {vm.DoneProblems.Count}, rapport : {vm.ReportPath}");

        Wait(vm.BackToListCommand.ExecuteAsync(null));
        Console.WriteLine($"nouvelle analyse : à copier {vm.ToCopyCount} (attendu 2 : {string.Join(", ", unchecked2.Select(u => u.Title))})");
        Console.WriteLine($"collections ensuite : {vm.CollectionsDetail} dispo={vm.CollectionsAvailable}");
    }

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
