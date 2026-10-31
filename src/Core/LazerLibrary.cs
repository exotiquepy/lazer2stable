using Realms;

namespace Lazer2Stable.Core;

public sealed record LazerFile(string Filename, string Hash);

public sealed record LazerBeatmap(int OnlineId, string Md5, string Hash, int RulesetId, string DifficultyName, double StarRating);

public sealed record LazerCollection(string Name, List<string> Md5s);

public sealed class LazerSet
{
    public Guid Id { get; init; }
    public int OnlineId { get; init; }
    public bool DeletePending { get; init; }
    public bool Protected { get; init; }
    public DateTimeOffset DateAdded { get; init; }

    /// <summary>Statut en ligne (BeatmapOnlineStatus de lazer) : 1 ranked, 2 approved, 3 qualified, 4 loved…</summary>
    public int Status { get; init; }

    public string Artist { get; set; } = "";
    public string Title { get; set; } = "";
    public string ArtistUnicode { get; set; } = "";
    public string TitleUnicode { get; set; } = "";
    public string Creator { get; set; } = "";
    public string BackgroundFile { get; set; } = "";
    public List<LazerBeatmap> Beatmaps { get; } = [];
    public List<LazerFile> Files { get; } = [];

    public string DisplayName
    {
        get
        {
            string name = $"{Artist} - {Title}".Trim(' ', '-');
            if (name.Length == 0) name = Id.ToString();
            if (Creator.Length > 0) name += $" ({Creator})";
            return OnlineId > 0 ? $"{OnlineId} {name}" : name;
        }
    }

    /// <summary>Hash du fichier image de fond dans le stockage lazer, s'il existe.</summary>
    public string? BackgroundHash()
    {
        if (BackgroundFile.Length > 0)
        {
            string wanted = BackgroundFile.Replace('\\', '/');
            var f = Files.FirstOrDefault(x => x.Filename.Replace('\\', '/').Equals(wanted, StringComparison.OrdinalIgnoreCase));
            if (f != null) return f.Hash;
        }

        return Files.FirstOrDefault(x => !x.Filename.Contains('/') &&
                                         (x.Filename.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                          x.Filename.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                          x.Filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))?.Hash;
    }
}

public sealed class LazerData
{
    public required List<LazerSet> Sets { get; init; }
    public required List<LazerCollection> Collections { get; init; }
    public List<LazerScore> Scores { get; init; } = [];
    public List<LazerSkin> Skins { get; init; } = [];
}

/// <summary>
/// Lit la base Realm de lazer en mode dynamique (sans les classes d'osu!), sur une copie temporaire,
/// pour ne jamais toucher au fichier d'origine.
/// </summary>
public static class LazerLibrary
{
    /// <param name="progress">Avancement de 0 à 1, et nombre de sets lus quand il est connu.</param>
    public static LazerData Read(LazerLocation lazer, IProgress<(double Fraction, int Sets, int Total)>? progress = null)
    {
        if (!Directory.Exists(lazer.FilesDir))
            throw new UserFacingException(Loc.F("ErrNoFilesDir", lazer.FilesDir));

        string tempDir = Path.Combine(Path.GetTempPath(), "lazer2stable-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string tempRealm = Path.Combine(tempDir, "client.realm");

        try
        {
            // FileShare.ReadWrite : la copie fonctionne même si lazer a le fichier ouvert.
            using (var src = new FileStream(lazer.RealmPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var dst = new FileStream(tempRealm, FileMode.CreateNew, FileAccess.Write))
            {
                // Copie par blocs pour suivre l'avancement (la base peut peser plusieurs centaines de Mo).
                long length = Math.Max(1, src.Length);
                var buffer = new byte[1 << 20];
                int read, blocks = 0;
                while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                {
                    dst.Write(buffer, 0, read);
                    if (++blocks % 8 == 0) progress?.Report((0.25 * dst.Position / length, 0, 0));
                }
            }

            return ReadFrom(tempRealm, progress);
        }
        finally
        {
            TryDelete(tempDir);
        }
    }

    private static LazerData ReadFrom(string realmPath, IProgress<(double Fraction, int Sets, int Total)>? progress)
    {
        // Mode dynamique : le schéma est lu depuis le fichier, aucune classe d'osu! n'est nécessaire.
        var config = new RealmConfiguration(realmPath) { IsDynamic = true };

        Realm realm;
        try
        {
            realm = Realm.GetInstance(config);
        }
        catch (Exception e)
        {
            throw new UserFacingException(Loc.F("ErrOpenRealm", e.Message));
        }

        using (realm)
        {
            var sets = new List<LazerSet>();
            var all = realm.DynamicApi.All("BeatmapSet");
            int total = Math.Max(1, all.Count());
            progress?.Report((0.25, 0, total));

            foreach (var s in all)
            {
                var api = s.DynamicApi;

                var set = new LazerSet
                {
                    Id = api.Get<Guid>("ID"),
                    OnlineId = GetInt(s, "OnlineID", -1),
                    DeletePending = GetBool(s, "DeletePending"),
                    Protected = GetBool(s, "Protected"),
                    Status = GetInt(s, "Status", -3),
                    DateAdded = GetDate(s, "DateAdded"),
                };

                foreach (var usage in api.GetList<IEmbeddedObject>("Files"))
                {
                    var file = usage.DynamicApi.Get<IRealmObject?>("File");
                    string? name = GetString(usage, "Filename");
                    if (file == null || string.IsNullOrEmpty(name)) continue;

                    string? hash = GetString(file, "Hash");
                    if (string.IsNullOrEmpty(hash) || hash.Length < 3) continue;

                    set.Files.Add(new LazerFile(name, hash.ToLowerInvariant()));
                }

                foreach (var b in api.GetList<IRealmObject>("Beatmaps"))
                {
                    var ruleset = b.DynamicApi.Get<IRealmObject?>("Ruleset");

                    set.Beatmaps.Add(new LazerBeatmap(
                        OnlineId: GetInt(b, "OnlineID", -1),
                        Md5: (GetString(b, "MD5Hash") ?? "").ToLowerInvariant(),
                        Hash: (GetString(b, "Hash") ?? "").ToLowerInvariant(),
                        RulesetId: ruleset == null ? -1 : GetInt(ruleset, "OnlineID", -1),
                        DifficultyName: GetString(b, "DifficultyName") ?? "",
                        StarRating: GetDouble(b, "StarRating", -1)));

                    if (set.Title.Length == 0)
                    {
                        var meta = b.DynamicApi.Get<IRealmObject?>("Metadata");
                        if (meta != null)
                        {
                            set.Title = GetString(meta, "Title") ?? "";
                            set.Artist = GetString(meta, "Artist") ?? "";
                            set.TitleUnicode = GetString(meta, "TitleUnicode") ?? "";
                            set.ArtistUnicode = GetString(meta, "ArtistUnicode") ?? "";
                            set.BackgroundFile = GetString(meta, "BackgroundFile") ?? "";
                            var author = meta.DynamicApi.Get<IEmbeddedObject?>("Author");
                            if (author != null)
                                set.Creator = GetString(author, "Username") ?? "";
                        }
                    }
                }

                sets.Add(set);
                if (sets.Count % 100 == 0) progress?.Report((0.25 + 0.75 * sets.Count / total, sets.Count, total));
            }

            progress?.Report((1, sets.Count, total));

            return new LazerData { Sets = sets, Collections = ReadCollections(realm), Scores = LazerScoreReader.Read(realm), Skins = LazerSkinReader.Read(realm) };
        }
    }

    private static List<LazerCollection> ReadCollections(Realm realm)
    {
        var result = new List<LazerCollection>();

        try
        {
            foreach (var c in realm.DynamicApi.All("BeatmapCollection"))
            {
                string name = GetString(c, "Name") ?? "";
                var hashes = c.DynamicApi.GetList<string>("BeatmapMD5Hashes")
                    .Where(h => !string.IsNullOrEmpty(h))
                    .Select(h => h.ToLowerInvariant())
                    .Distinct()
                    .ToList();

                if (name.Length > 0)
                    result.Add(new LazerCollection(name, hashes));
            }
        }
        catch
        {
            // Pas de collections (ou schéma différent) : on continue sans.
        }

        return result;
    }

    private static string? GetString(IRealmObjectBase o, string prop)
    {
        try { return o.DynamicApi.Get<string?>(prop); }
        catch { return null; }
    }

    private static int GetInt(IRealmObjectBase o, string prop, int fallback)
    {
        try { return (int)o.DynamicApi.Get<long>(prop); }
        catch { return fallback; }
    }

    private static double GetDouble(IRealmObjectBase o, string prop, double fallback)
    {
        try { return o.DynamicApi.Get<double>(prop); }
        catch { return fallback; }
    }

    private static bool GetBool(IRealmObjectBase o, string prop)
    {
        try { return o.DynamicApi.Get<bool>(prop); }
        catch { return false; }
    }

    private static DateTimeOffset GetDate(IRealmObjectBase o, string prop)
    {
        try { return o.DynamicApi.Get<DateTimeOffset>(prop); }
        catch { return DateTimeOffset.MinValue; }
    }

    private static void TryDelete(string dir)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                return;
            }
            catch
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(200);
            }
        }
    }
}

/// <summary>Erreur dont le message est destiné à l'utilisateur tel quel.</summary>
public sealed class UserFacingException(string message) : Exception(message);
