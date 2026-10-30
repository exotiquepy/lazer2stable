// Rend chaque écran d'Exotique hors écran (Avalonia headless + Skia) et enregistre des PNG.
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Exotique.App;
using Exotique.App.ViewModels;
using Exotique.Core;

internal static class Shots
{
    private static void Main(string[] args)
    {
        string lazerDir = args[0], stableDir = args[1], outDir = args[2];
        int width = args.Length > 3 ? int.Parse(args[3]) : 1280;
        int height = args.Length > 4 ? int.Parse(args[4]) : 820;
        Directory.CreateDirectory(outDir);

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        if (args.Length > 5 && args[5] == "e2e")
        {
            E2E.Run(lazerDir, stableDir);
            return;
        }

        foreach (var lang in new[] { Lang.Fr, Lang.En, Lang.Ru })
            RunLanguage(lang, Path.Combine(outDir, Loc.Code(lang)), lazerDir, stableDir, width, height);
        Console.WriteLine("captures : " + Directory.GetFiles(outDir, "*.png", SearchOption.AllDirectories).Length);
    }

    private static void RunLanguage(Lang lang, string outDir, string lazerDir, string stableDir, int width, int height)
    {
        Directory.CreateDirectory(outDir);

        // La langue mémorisée dans les réglages l'emporte au démarrage : on la fixe là aussi.
        var saved = Settings.Load();
        saved.Language = Loc.Code(lang);
        saved.CopyBeatmaps = true;
        saved.CopyScores = true;
        saved.ScoresIncludeOthers = false;
        saved.Save();
        Loc.I.Lang = lang;

        // Réglages : écran de choix des dossiers (rien de trouvé)
        var setupVm = new MainViewModel();
        setupVm.TestUseLocations(null, null);
        setupVm.TestSetScreen(Screen.Setup);
        var setupWin = new MainWindow(setupVm, autoStart: false) { Width = width, Height = height };
        setupWin.Show();
        Pump(0.5);
        Save(setupWin, outDir, "1-reglages");
        setupWin.Close();

        var vm = new MainViewModel();
        vm.TestUseLocations(Paths.LazerAt(lazerDir), Paths.StableAt(stableDir));
        var w = new MainWindow(vm, autoStart: false) { Width = width, Height = height };
        w.Show();

        vm.TestSetScreen(Screen.Analyzing);
        Pump(0.6);
        Save(w, outDir, "2-analyse");
        var t = vm.AnalyzeCommand.ExecuteAsync(null);
        while (!t.IsCompleted) Pump(0.05);
        if (t.IsFaulted) Console.WriteLine(t.Exception);
        Pump(2.0);
        Save(w, outDir, "3-liste");

        if (lang == Lang.Fr)
        {
            // Bascule de langue à chaud : toute la liste doit passer en russe sans relancer.
            vm.TestSetLanguage(Lang.Ru);
            Pump(0.8);
            Save(w, outDir, "3b-liste-bascule-ru");
            vm.TestSetLanguage(Lang.Fr);
            Pump(0.5);
        }

        // Scores : à copier, déjà là, ignorés, puis « seulement les scores ».
        vm.TestSetSection(Section.Scores);
        Pump(1.2);
        Save(w, outDir, "3c-scores");
        vm.SelectedScoreTab = ListTab.Present;
        Pump(0.8);
        Save(w, outDir, "3d-scores-deja-la");
        vm.SelectedScoreTab = ListTab.Ignored;
        Pump(0.8);
        Save(w, outDir, "3e-scores-ignores");
        vm.SelectedScoreTab = ListTab.ToCopy;
        vm.IncludeMaps = false;
        Pump(0.8);
        Save(w, outDir, "3f-scores-seulement");
        vm.TestOpenConfirm();
        Pump(0.5);
        Save(w, outDir, "3g-confirmation-scores");
        vm.CancelExportCommand.Execute(null);
        vm.IncludeMaps = true;
        vm.TestSetSection(Section.Maps);
        vm.IncludeScores = false;
        Pump(0.6);
        Save(w, outDir, "3h-maps-sans-scores");
        vm.IncludeScores = true;
        Pump(0.4);

        vm.SearchText = "lumen";
        Pump(0.8);
        Save(w, outDir, "4-recherche");
        vm.SearchText = "";
        Pump(0.5);

        vm.SelectedTab = ListTab.Present;
        Pump(1.0);
        Save(w, outDir, "5-deja-la");

        vm.SelectedTab = ListTab.Ignored;
        Pump(0.6);
        Save(w, outDir, "6-ignorees");

        vm.SelectedTab = ListTab.ToCopy;
        Pump(0.6);
        vm.TestOpenConfirm();
        Pump(0.6);
        Save(w, outDir, "7-confirmation");
        vm.TestSetOsu(true, false);
        Pump(0.4);
        Save(w, outDir, "7b-confirmation-osu-ouvert");
        vm.TestSetOsu(true, true);
        Pump(0.4);
        Save(w, outDir, "7c-confirmation-osu-bloque");
        vm.CancelExportCommand.Execute(null);
        vm.TestSetOsu(true, false);
        Pump(0.4);
        Save(w, outDir, "7d-bandeau-osu-ouvert");
        vm.TestSetOsu(false, false);
        vm.ShowError("Accès refusé au chemin « C:\\osu!\\Songs\\1234 Test ».", "/tmp/erreur.txt");
        Pump(0.4);
        Save(w, outDir, "7e-erreur");
        vm.DismissErrorCommand.Execute(null);

        var first = vm.VisibleItems.First(i => i.IsCopyCandidate);
        _ = first.Thumbnail;
        Pump(0.4);
        vm.TestSetExporting(first, 31, 48);
        Pump(1.2);
        Save(w, outDir, "8-copie");

        vm.TestShowDone(48, 17);
        Pump(1.5);
        Save(w, outDir, "9-bilan");

    }

    private static void Pump(double seconds)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        do
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(15);
        } while (DateTime.UtcNow < end);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Avalonia.Controls.Window w, string dir, string name)
    {
        var frame = w.CaptureRenderedFrame();
        frame?.Save(Path.Combine(dir, name + ".png"));
    }
}
