// Images du README : captures fixes (docs/) et images de l'animation (frames/ + list.txt pour ffmpeg).
using Avalonia.Headless;
using Avalonia.Threading;
using Lazer2Stable.App;
using Lazer2Stable.App.ViewModels;
using Lazer2Stable.Core;

internal static class Showcase
{
    public static void Run(string lazerDir, string stableDir, string outDir, int width, int height)
    {
        string framesDir = Path.Combine(outDir, "frames");
        Directory.CreateDirectory(framesDir);
        var list = new List<string>();
        int n = 0;

        var saved = Settings.Load();
        saved.Language = "en";
        saved.CopyBeatmaps = saved.CopyScores = saved.CopySkins = true;
        saved.ScoresIncludeOthers = false;
        saved.ExportAsOsz = false;
        saved.Save();
        Loc.I.Lang = Lang.En;

        MainViewModel.TestShowPath = p => p
            .Replace(Path.GetFullPath(lazerDir).TrimEnd('/'), @"C:\Users\you\AppData\Roaming\osu")
            .Replace(Path.GetFullPath(stableDir).TrimEnd('/'), @"C:\Users\you\AppData\Local\osu!")
            .Replace('/', '\\');

        var vm = new MainViewModel();
        vm.TestUseLocations(Paths.LazerAt(lazerDir), Paths.StableAt(stableDir));
        var w = new MainWindow(vm, autoStart: false) { Width = width, Height = height };
        w.Show();

        void Frame(double seconds)
        {
            string name = $"{n++:000}.png";
            w.CaptureRenderedFrame()?.Save(Path.Combine(framesDir, name));
            list.Add($"file '{name}'\nduration {seconds:0.###}".Replace(',', '.'));
        }

        void Still(string name) => w.CaptureRenderedFrame()?.Save(Path.Combine(outDir, name + ".png"));

        Analyze(vm);
        int sets = vm.ToCopyCount + vm.PresentCount + vm.IgnoredCount;
        int scores = vm.ScoreToCopyCount + vm.ScorePresentCount + vm.ScoreIgnoredCount;
        int skins = vm.SkinToCopyCount + vm.SkinPresentCount + vm.SkinIgnoredCount;
        int diffs = Directory.GetFiles(stableDir, "*.osu", SearchOption.AllDirectories).Length;
        int mapsOut = vm.ToCopyCount, scoresOut = vm.ScoreToCopyCount, skinsOut = vm.SkinToCopyCount;

        // 1. Analyse
        const int steps = 26;
        for (int k = 0; k <= steps; k++)
        {
            double t = (double)k / steps;
            vm.TestAnalysisFrame(1 - Math.Pow(1 - t, 1.6), sets, scores, skins, diffs, mapsOut, scoresOut, skinsOut);
            Pump(0.07);
            Frame(k == steps ? 0.7 : 0.08);
        }

        // 2. Les trois listes
        Analyze(vm);
        foreach (var i in vm.VisibleItems.Take(40)) _ = i.Thumbnail;
        Pump(2.5);
        Frame(2.8);
        Still("screenshot-list");

        vm.TestSetSection(Section.Scores);
        Pump(1.2);
        Frame(2.4);
        Still("screenshot-scores");

        vm.TestSetSection(Section.Skins);
        Pump(1.8);
        Frame(2.4);
        Still("screenshot-skins");

        // 3. Confirmation
        vm.TestSetSection(Section.Maps);
        Pump(0.5);
        vm.TestOpenConfirm();
        Pump(0.6);
        Frame(2.6);
        Still("screenshot-confirm");
        vm.CancelExportCommand.Execute(null);

        // 4. Copie
        var maps = vm.VisibleItems.Where(i => i.IsCopyCandidate && i.IsSelected).ToList();
        int total = maps.Count;
        long bytesTotal = maps.Sum(i => i.Item.Bytes);
        const int copySteps = 30;
        for (int k = 1; k <= copySteps; k++)
        {
            int done = Math.Max(1, total * k / copySteps);
            var current = maps[Math.Min(done, total - 1)];
            _ = current.Thumbnail;
            var recent = maps.Take(done).Reverse().Take(9).ToList();
            vm.TestExportFrame(current, done, total, maps.Take(done).Sum(i => i.Item.Bytes), bytesTotal,
                TimeSpan.FromSeconds(140.0 * (total - done) / total), recent);
            Pump(0.08);
            Frame(0.09);
        }

        // 5. Bilan
        vm.TestSetLastCopy(new CopyJournal { Maps = ["x"] });
        vm.TestShowDone(total, scoresOut, skinsOut, warning: false, bytes: bytesTotal, seconds: 138);
        Pump(1.5);
        Frame(4.0);
        Still("screenshot-done");

        list.Add(list[^1].Split('\n')[0]); // ffmpeg : la dernière image doit être répétée pour garder sa durée
        File.WriteAllText(Path.Combine(framesDir, "list.txt"), string.Join("\n", list) + "\n");
        Console.WriteLine($"showcase : {n} images, {mapsOut} maps / {scoresOut} scores / {skinsOut} skins, {Fmt.Bytes(bytesTotal)}");
    }

    private static void Analyze(MainViewModel vm)
    {
        var t = vm.AnalyzeCommand.ExecuteAsync(null);
        while (!t.IsCompleted) Pump(0.05);
        if (t.IsFaulted) throw t.Exception!;
        Pump(0.5);
    }

    private static void Pump(double seconds)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        do
        {
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(12);
        } while (DateTime.UtcNow < end);
        Dispatcher.UIThread.RunJobs();
    }
}
