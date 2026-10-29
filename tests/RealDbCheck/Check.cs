using Exotique.Core;

internal static class Check
{
    private static void Main(string[] args)
    {
        var loc = new LazerLocation(args[0], Path.Combine(args[0], "client.realm"));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var data = LazerLibrary.Read(loc);
        var sets = data.Sets;
        Console.WriteLine($"sets={sets.Count} en {sw.ElapsedMilliseconds} ms");
        var live = sets.Where(s => !s.DeletePending && !s.Protected).ToList();
        var maps = live.SelectMany(s => s.Beatmaps).ToList();
        Console.WriteLine($"live={live.Count} beatmaps={maps.Count} fichiers={live.Sum(s => s.Files.Count)}");
        Console.WriteLine($"étoiles : min {maps.Min(b => b.StarRating):0.00} max {maps.Max(b => b.StarRating):0.00} moyenne {maps.Average(b => b.StarRating):0.00}, inconnues (<0) : {maps.Count(b => b.StarRating < 0)}");
        Console.WriteLine($"fond : BackgroundFile renseigné {live.Count(s => s.BackgroundFile.Length > 0)}, image trouvée {live.Count(s => s.BackgroundHash() != null)}");
        Console.WriteLine("statuts : " + string.Join(", ", live.GroupBy(s => s.Status).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
        Console.WriteLine($"DateAdded : {live.Min(s => s.DateAdded):yyyy-MM-dd} -> {live.Max(s => s.DateAdded):yyyy-MM-dd}, vides {live.Count(s => s.DateAdded == DateTimeOffset.MinValue)}");
        Console.WriteLine($"unicode : {live.Count(s => s.TitleUnicode.Length > 0 && s.TitleUnicode != s.Title)} titres unicode différents");
        Console.WriteLine($"collections : {data.Collections.Count}, maps dedans : {data.Collections.Sum(c => c.Md5s.Count)}, md5 de 32 car. : {data.Collections.SelectMany(c => c.Md5s).All(h => h.Length == 32)}");
        var md5s = maps.Select(b => b.Md5).ToHashSet();
        Console.WriteLine($"maps des collections présentes dans lazer : {data.Collections.SelectMany(c => c.Md5s).Count(md5s.Contains)}");
        var bgs = live.Select(s => s.BackgroundHash()).Where(h => h != null).Take(3).ToList();
        File.WriteAllLines(Path.Combine(args[0], "sample-bg.txt"), bgs!);
    }
}
