// Génère une fausse bibliothèque lazer (client.realm au schéma d'osu!, + files/) et un faux dossier Songs stable,
// avec un cas de test par situation. Usage : FakeLibrary <dossier-sortie> [nombre-de-sets-en-masse]
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Exotique.Core;
using Realms;
using Realms.Schema;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : "fake");
int bulk = args.Length > 1 ? int.Parse(args[1]) : 1500;

if (Directory.Exists(root)) Directory.Delete(root, true);
string lazerDir = Path.Combine(root, "lazer");
string songs = Path.Combine(root, "stable", "Songs");
Directory.CreateDirectory(Path.Combine(lazerDir, "files"));
Directory.CreateDirectory(songs);

var schema = new RealmSchema.Builder
{
    new ObjectSchema.Builder("File", ObjectSchema.ObjectType.RealmObject)
    {
        Property.Primitive("Hash", RealmValueType.String, isPrimaryKey: true),
    },
    new ObjectSchema.Builder("RealmNamedFileUsage", ObjectSchema.ObjectType.EmbeddedObject)
    {
        Property.Object("File", "File"),
        Property.Primitive("Filename", RealmValueType.String),
    },
    new ObjectSchema.Builder("RealmUser", ObjectSchema.ObjectType.EmbeddedObject)
    {
        Property.Primitive("OnlineID", RealmValueType.Int),
        Property.Primitive("Username", RealmValueType.String),
        Property.Primitive("CountryCode", RealmValueType.String),
    },
    new ObjectSchema.Builder("Ruleset", ObjectSchema.ObjectType.RealmObject)
    {
        Property.Primitive("ShortName", RealmValueType.String, isPrimaryKey: true),
        Property.Primitive("OnlineID", RealmValueType.Int, indexType: IndexType.General),
        Property.Primitive("Name", RealmValueType.String),
        Property.Primitive("InstantiationInfo", RealmValueType.String),
        Property.Primitive("Available", RealmValueType.Bool),
    },
    new ObjectSchema.Builder("BeatmapDifficulty", ObjectSchema.ObjectType.EmbeddedObject)
    {
        Property.Primitive("DrainRate", RealmValueType.Float),
        Property.Primitive("CircleSize", RealmValueType.Float),
        Property.Primitive("OverallDifficulty", RealmValueType.Float),
        Property.Primitive("ApproachRate", RealmValueType.Float),
        Property.Primitive("SliderMultiplier", RealmValueType.Double),
        Property.Primitive("SliderTickRate", RealmValueType.Double),
    },
    new ObjectSchema.Builder("BeatmapMetadata", ObjectSchema.ObjectType.RealmObject)
    {
        Property.Primitive("Title", RealmValueType.String),
        Property.Primitive("TitleUnicode", RealmValueType.String),
        Property.Primitive("Artist", RealmValueType.String),
        Property.Primitive("ArtistUnicode", RealmValueType.String),
        Property.Object("Author", "RealmUser"),
        Property.Primitive("Source", RealmValueType.String),
        Property.Primitive("Tags", RealmValueType.String),
        Property.PrimitiveList("UserTags", RealmValueType.String),
        Property.Primitive("PreviewTime", RealmValueType.Int),
        Property.Primitive("AudioFile", RealmValueType.String),
        Property.Primitive("BackgroundFile", RealmValueType.String),
    },
    new ObjectSchema.Builder("Beatmap", ObjectSchema.ObjectType.RealmObject)
    {
        Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
        Property.Primitive("DifficultyName", RealmValueType.String),
        Property.Object("Ruleset", "Ruleset"),
        Property.Object("Difficulty", "BeatmapDifficulty"),
        Property.Object("Metadata", "BeatmapMetadata"),
        Property.Object("BeatmapSet", "BeatmapSet"),
        Property.Primitive("Status", RealmValueType.Int),
        Property.Primitive("OnlineID", RealmValueType.Int, indexType: IndexType.General),
        Property.Primitive("Length", RealmValueType.Double),
        Property.Primitive("BPM", RealmValueType.Double),
        Property.Primitive("Hash", RealmValueType.String),
        Property.Primitive("StarRating", RealmValueType.Double),
        Property.Primitive("MD5Hash", RealmValueType.String, indexType: IndexType.General),
        Property.Primitive("OnlineMD5Hash", RealmValueType.String),
        Property.Primitive("LastLocalUpdate", RealmValueType.Date, isNullable: true),
        Property.Primitive("Hidden", RealmValueType.Bool),
    },
    new ObjectSchema.Builder("BeatmapSet", ObjectSchema.ObjectType.RealmObject)
    {
        Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
        Property.Primitive("OnlineID", RealmValueType.Int, indexType: IndexType.General),
        Property.Primitive("DateAdded", RealmValueType.Date),
        Property.Primitive("DateSubmitted", RealmValueType.Date, isNullable: true),
        Property.ObjectList("Beatmaps", "Beatmap"),
        Property.ObjectList("Files", "RealmNamedFileUsage"),
        Property.Primitive("Status", RealmValueType.Int),
        Property.Primitive("DeletePending", RealmValueType.Bool),
        Property.Primitive("Hash", RealmValueType.String),
        Property.Primitive("Protected", RealmValueType.Bool),
    },
    new ObjectSchema.Builder("BeatmapCollection", ObjectSchema.ObjectType.RealmObject)
    {
        Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
        Property.Primitive("Name", RealmValueType.String),
        Property.PrimitiveList("BeatmapMD5Hashes", RealmValueType.String),
        Property.Primitive("LastModified", RealmValueType.Date),
    },
    new ObjectSchema.Builder("Score", ObjectSchema.ObjectType.RealmObject)
    {
        Property.Primitive("ID", RealmValueType.Guid, isPrimaryKey: true),
        Property.Object("BeatmapInfo", "Beatmap"),
        Property.Primitive("BeatmapHash", RealmValueType.String),
        Property.Object("Ruleset", "Ruleset"),
        Property.ObjectList("Files", "RealmNamedFileUsage"),
        Property.Primitive("Hash", RealmValueType.String),
        Property.Primitive("DeletePending", RealmValueType.Bool),
        Property.Primitive("TotalScore", RealmValueType.Int),
        Property.Primitive("TotalScoreVersion", RealmValueType.Int),
        Property.Primitive("LegacyTotalScore", RealmValueType.Int, isNullable: true),
        Property.Primitive("MaxCombo", RealmValueType.Int),
        Property.Primitive("Accuracy", RealmValueType.Double),
        Property.Primitive("Date", RealmValueType.Date),
        Property.Primitive("OnlineID", RealmValueType.Int),
        Property.Primitive("LegacyOnlineID", RealmValueType.Int),
        Property.Object("User", "RealmUser"),
        Property.Primitive("Mods", RealmValueType.String),
        Property.Primitive("Statistics", RealmValueType.String),
        Property.Primitive("MaximumStatistics", RealmValueType.String),
        Property.Primitive("Rank", RealmValueType.Int),
        Property.Primitive("IsLegacyScore", RealmValueType.Bool),
    },
};

var config = new RealmConfiguration(Path.Combine(lazerDir, "client.realm"))
{
    Schema = schema,
    IsDynamic = true,
    SchemaVersion = 52,
};

var expected = new List<string>();
var rnd = new Random(42);

using (var realm = Realm.GetInstance(config))
{
    realm.Write(() =>
    {
        var rulesets = new Dictionary<string, IRealmObject>();
        foreach (var (shortName, id) in new[] { ("osu", 0), ("taiko", 1), ("fruits", 2), ("mania", 3), ("tau", -1) })
        {
            var r = (IRealmObject)realm.DynamicApi.CreateObject("Ruleset", shortName);
            r.DynamicApi.Set("OnlineID", id);
            r.DynamicApi.Set("Name", shortName);
            r.DynamicApi.Set("Available", true);
            rulesets[shortName] = r;
        }

        // ------------------------------------------------------------------ helpers

        IRealmObject AddFile(byte[] content)
        {
            string hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            string dir = Path.Combine(lazerDir, "files", hash[..1], hash[..2]);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, hash), content);
            return (IRealmObject?)realm.DynamicApi.Find("File", hash) ?? (IRealmObject)realm.DynamicApi.CreateObject("File", hash);
        }

        IRealmObject MakeSet(int onlineId, string artist, string title, string creator, (string name, byte[] content)[] files,
            (string diff, int beatmapId, string osuFile, string ruleset)[] diffs, bool deletePending = false, bool isProtected = false,
            string[]? removeFromStore = null, double[]? stars = null, string? background = null, int status = 1, int ageDays = 900)
        {
            var set = (IRealmObject)realm.DynamicApi.CreateObject("BeatmapSet", Guid.NewGuid());
            set.DynamicApi.Set("OnlineID", onlineId);
            set.DynamicApi.Set("DateAdded", DateTimeOffset.UtcNow.AddDays(-ageDays));
            set.DynamicApi.Set("Status", onlineId > 0 ? status : -3);
            set.DynamicApi.Set("DeletePending", deletePending);
            set.DynamicApi.Set("Protected", isProtected);
            set.DynamicApi.Set("Hash", Guid.NewGuid().ToString("N"));

            var meta = (IRealmObject)realm.DynamicApi.CreateObject("BeatmapMetadata");
            meta.DynamicApi.Set("Title", title);
            meta.DynamicApi.Set("Artist", artist);
            if (background != null) meta.DynamicApi.Set("BackgroundFile", background);
            var author = realm.DynamicApi.CreateEmbeddedObjectForProperty(meta, "Author");
            author.DynamicApi.Set("Username", creator);
            author.DynamicApi.Set("OnlineID", 2);

            var fileList = set.DynamicApi.GetList<IEmbeddedObject>("Files");
            var hashes = new Dictionary<string, string>();
            foreach (var (name, content) in files)
            {
                var f = AddFile(content);
                var usage = realm.DynamicApi.AddEmbeddedObjectToList(fileList);
                usage.DynamicApi.Set("File", RealmValue.Object(f));
                usage.DynamicApi.Set("Filename", name);
                hashes[name] = f.DynamicApi.Get<string>("Hash");
            }

            var beatmaps = set.DynamicApi.GetList<IRealmObject>("Beatmaps");
            int di = 0;
            foreach (var (diff, beatmapId, osuFile, ruleset) in diffs)
            {
                double sr = stars != null && di < stars.Length ? stars[di] : 1.5 + di * 1.3;
                di++;
                byte[] content = files.First(f => f.name == osuFile).content;
                var b = (IRealmObject)realm.DynamicApi.CreateObject("Beatmap", Guid.NewGuid());
                b.DynamicApi.Set("DifficultyName", diff);
                b.DynamicApi.Set("OnlineID", beatmapId);
                b.DynamicApi.Set("Ruleset", RealmValue.Object(rulesets[ruleset]));
                b.DynamicApi.Set("Metadata", RealmValue.Object(meta));
                b.DynamicApi.Set("BeatmapSet", RealmValue.Object(set));
                b.DynamicApi.Set("Hash", hashes[osuFile]);
                b.DynamicApi.Set("MD5Hash", Convert.ToHexString(MD5.HashData(content)).ToLowerInvariant());
                b.DynamicApi.Set("StarRating", sr);
                var d = realm.DynamicApi.CreateEmbeddedObjectForProperty(b, "Difficulty");
                d.DynamicApi.Set("CircleSize", 4f);
                beatmaps.Add(b);
            }

            foreach (string name in removeFromStore ?? [])
            {
                string h = hashes[name];
                File.Delete(Path.Combine(lazerDir, "files", h[..1], h[..2], h));
            }

            return set;
        }

        // ------------------------------------------------------------- cas précis

        // 1001 : présent dans stable via le préfixe du dossier
        var osuA = Osu(14, "A", 5001, 1001);
        MakeSet(1001, "Artist A", "Title A", "mapperA", [("audio.mp3", Bin("audioA")), ("a.osu", osuA)], [("Normal", 5001, "a.osu", "osu")]);
        StableFolder("1001 Artist A - Title A", ("a-old.osu", Osu(14, "A old", 5001, 1001)), ("audio.mp3", Bin("audioA")));
        expected.Add("1001 present");

        // 1002 : présent via BeatmapSetID dans le .osu (dossier sans ID)
        MakeSet(1002, "Artist B", "Title B", "mapperB", [("audio.mp3", Bin("audioB")), ("b.osu", Osu(14, "B", 5002, 1002))], [("Hard", 5002, "b.osu", "osu")]);
        StableFolder("Renamed folder B", ("b-stable.osu", Osu(14, "B stable", -1, 1002)));
        expected.Add("1002 present");

        // local : non soumise, présente via MD5 identique
        var osuC = Osu(14, "C local", -1, -1);
        MakeSet(-1, "Artist C", "Local C", "me", [("audio.ogg", Bin("audioC")), ("c.osu", osuC)], [("Mine", -1, "c.osu", "osu")]);
        StableFolder("Local C", ("c.osu", osuC));
        expected.Add("LocalC present");

        // 1004 : manquante, avec storyboard en sous-dossier, .osb, hitsound, 2 difficultés (dont mania)
        MakeSet(1004, "Artist D", "Title D", "Mapper",
            [("audio.mp3", Bin("audioD", 300_000)), ("bg.jpg", Bin("bgD", 50_000)), ("sb/star.png", Bin("star")), ("sb/deep/glow.png", Bin("glow")),
             ("Artist D - Title D (Mapper).osb", Encoding.UTF8.GetBytes("[Events]\nSprite,Foreground,Centre,\"sb/star.png\",320,240\n")),
             ("soft-hitclap2.wav", Bin("clap")), ("d-easy.osu", Osu(14, "Easy", 5004, 1004)), ("d-4k.osu", Osu(14, "4K", 5005, 1004))],
            [("Easy", 5004, "d-easy.osu", "osu"), ("4K", 5005, "d-4k.osu", "mania")]);
        expected.Add("1004 copy");

        // 1005 : manquante, caractères interdits sous Windows
        MakeSet(1005, "AC/DC", "Title: \"E\" <x>?", "m*pper", [("audio.mp3", Bin("audioE")), ("e.osu", Osu(14, "E", 5006, 1005))], [("Insane", 5006, "e.osu", "taiko")]);
        expected.Add("1005 copy (nom nettoyé)");

        // locale v128 : modifiée dans l'éditeur lazer
        var setF = MakeSet(-1, "Artist F", "Edited F", "me", [("audio.mp3", Bin("audioF")), ("f.osu", Osu(128, "F", -1, -1))], [("Edit", -1, "f.osu", "osu")]);
        expected.Add("EditedF lazer-edited");

        // 1007 : supprimée dans lazer
        MakeSet(1007, "Artist G", "Deleted G", "x", [("audio.mp3", Bin("audioG")), ("g.osu", Osu(14, "G", 5007, 1007))], [("X", 5007, "g.osu", "osu")], deletePending: true);
        expected.Add("1007 skipped (deleted)");

        // protégée (contenu interne lazer)
        MakeSet(-1, "osu!", "Triangles intro", "peppy", [("audio.mp3", Bin("audioH")), ("h.osu", Osu(14, "H", -1, -1))], [("Intro", -1, "h.osu", "osu")], isProtected: true);
        expected.Add("Intro skipped (protected)");

        // 1009 : mode de jeu custom uniquement
        var setI = MakeSet(1009, "Artist I", "Tau I", "x", [("audio.mp3", Bin("audioI")), ("i.osu", Osu(14, "I", 5009, 1009))], [("Tau", 5009, "i.osu", "tau")]);
        expected.Add("1009 unsupported-ruleset");

        // 1010 : .osu absent du stockage lazer
        MakeSet(1010, "Artist J", "Broken J", "x", [("audio.mp3", Bin("audioJ")), ("j.osu", Osu(14, "J", 5010, 1010))], [("J", 5010, "j.osu", "osu")], removeFromStore: ["j.osu"]);
        expected.Add("1010 missing-files");

        // 1011 : audio absent du stockage -> copiée avec avertissement
        MakeSet(1011, "Artist K", "No audio K", "x", [("audio.mp3", Bin("audioK")), ("k.osu", Osu(14, "K", 5011, 1011))], [("K", 5011, "k.osu", "osu")], removeFromStore: ["audio.mp3"]);
        expected.Add("1011 copy + warning");

        // 1012 : présente en .osz dans Songs
        MakeSet(1012, "Artist L", "Osz L", "x", [("audio.mp3", Bin("audioL")), ("l.osu", Osu(14, "L", 5012, 1012))], [("L", 5012, "l.osu", "osu")]);
        using (var zip = ZipFile.Open(Path.Combine(songs, "1012 Artist L - Osz L.osz"), ZipArchiveMode.Create))
        {
            using var w = new StreamWriter(zip.CreateEntry("l.osu").Open());
            w.Write(Encoding.UTF8.GetString(Osu(14, "L", 5012, 1012)));
        }
        expected.Add("1012 present (.osz)");

        // 1013 : nom de fichier malveillant
        MakeSet(1013, "Artist M", "Evil M", "x", [("audio.mp3", Bin("audioM")), ("../../evil.txt", Bin("evil")), ("m.osu", Osu(14, "M", 5013, 1013))], [("M", 5013, "m.osu", "osu")]);
        expected.Add("1013 copy + warning (../../evil.txt refusé)");

        // 1014 : présente via BeatmapID seulement
        MakeSet(1014, "Artist N", "ByBeatmapId N", "x", [("audio.mp3", Bin("audioN")), ("n.osu", Osu(14, "N new", 50014, 1014))], [("N", 50014, "n.osu", "osu")]);
        StableFolder("Old N", ("n-old.osu", Osu(9, "N old", 50014, -1)));
        expected.Add("1014 present (BeatmapID)");

        // 1015 : en double dans lazer -> copiée une seule fois
        MakeSet(1015, "Artist O", "Dup O", "x", [("audio.mp3", Bin("audioO")), ("o.osu", Osu(14, "O", 5015, 1015))], [("O", 5015, "o.osu", "osu")]);
        MakeSet(1015, "Artist O", "Dup O", "x", [("audio.mp3", Bin("audioO")), ("o.osu", Osu(14, "O", 5015, 1015))], [("O", 5015, "o.osu", "osu")]);
        expected.Add("1015 copy once");

        // 1016 : manquante mais un dossier du même nom existe déjà (autre map) -> « (2) »
        MakeSet(1016, "Artist P", "Clash P", "x", [("audio.mp3", Bin("audioP")), ("p.osu", Osu(14, "P", 5016, 1016))], [("P", 5016, "p.osu", "osu")]);
        StableFolder("1016 Artist P - Clash P", ("unrelated.txt", Bin("x")));
        // (le préfixe 1016 du dossier existant fait qu'elle est considérée présente : comportement attendu)
        expected.Add("1016 present (préfixe de dossier)");

        // ------------------------------------------------------------- vitrine
        // Des maps aux noms inventés, avec images de fond, étoiles et statuts variés, pour les captures d'écran.

        string? bgDir = args.Length > 2 ? args[2] : null;
        var bgs = bgDir != null && Directory.Exists(bgDir) ? Directory.GetFiles(bgDir, "*.jpg").OrderBy(f => f).ToArray() : [];
        string[] artists = ["Kairo Nami", "Velvet Arcade", "Shiori Lumen", "neon parade", "Haruka Sora", "Glass Pilot", "Mirai Echo",
            "Lowtide Club", "Yume no Kaze", "Static Bloom", "Aoi Rhythm", "Paper Comet", "Ruri & The Hours", "Midnight Kiln", "Tsukimi"];
        string[] titles = ["Lumen Drive", "Afterglow Circuit", "Kaleidoscope Rain", "Starfall Protocol", "Hanabi no Yoru", "Second Horizon",
            "Prism Heart", "Ocean Signal", "Clockwork Sakura", "Neon Cathedral", "Paper Planes Forever", "Midnight Express",
            "Crystal Overdrive", "Fragments of Blue", "Hyperbloom", "Silent Voltage", "Tidal Memory", "Aurora Sprint", "Velocity Garden",
            "Moonlit Arcade", "Glitter Storm", "Echoes in Orbit", "Cherry Static", "Satellite Heart"];
        string[] mappers = ["fieldnote", "Kazeha", "plumcake", "Ryuu", "velvetpaw", "Noxi", "sakuraba", "Tenshi", "byte_orchid", "Mira"];
        string[] diffNames = ["Easy", "Normal", "Hard", "Insane", "Expert", "Extra", "Another", "Lunatic", "Extreme"];
        int[] statuses = [1, 1, 1, 4, 1, 3, 4, 1, -2, 1, 2, 1];
        var showcaseMd5 = new List<List<string>>();
        var showcaseSets = new List<IRealmObject>();

        for (int i = 0; i < (bgs.Length > 0 ? 72 : 0); i++)
        {
            int setId = 3_100_000 + i * 137;
            string artist = artists[i % artists.Length];
            string title = titles[(i * 7) % titles.Length] + (i >= titles.Length ? $" ({(i % 2 == 0 ? "TV Size" : "Extended Mix")})" : "");
            string mapper = mappers[(i * 3) % mappers.Length];
            int nd = 1 + (i * 5 % 7);
            if (i % 9 == 0) nd = 11 + i % 4;
            string mode = i % 11 == 3 ? "mania" : i % 13 == 5 ? "taiko" : i % 17 == 7 ? "fruits" : "osu";

            var files = new List<(string, byte[])>
            {
                ("audio.mp3", Bin("showaudio" + i, 120_000 + (i * 7919) % 900_000)),
                ("bg.jpg", File.ReadAllBytes(bgs[i % bgs.Length])),
            };
            var d = new List<(string, int, string, string)>();
            var sr = new List<double>();
            for (int k = 0; k < nd; k++)
            {
                string dn = nd > diffNames.Length ? $"{diffNames[k % diffNames.Length]} {k / diffNames.Length + 1}" : diffNames[k];
                string fn = $"{artist} - {title} ({mapper}) [{dn}].osu";
                files.Add((fn, Osu(14, dn, setId * 10 + k, setId)));
                d.Add((dn, setId * 10 + k, fn, k == nd - 1 && i % 5 == 1 ? "mania" : mode));
                sr.Add(Math.Round(1.2 + k * (6.8 / Math.Max(1, nd - 1)) + (i % 4) * 0.25, 2));
            }

            var set = MakeSet(setId, artist, title, mapper, files.ToArray(), d.ToArray(), stars: sr.ToArray(), background: "bg.jpg",
                status: statuses[i % statuses.Length], ageDays: i * 3);
            showcaseMd5.Add(set.DynamicApi.GetList<IRealmObject>("Beatmaps").Select(b => b.DynamicApi.Get<string>("MD5Hash")).ToList());
            showcaseSets.Add(set);

            if (i % 3 == 2)
                StableFolder($"{setId} {artist} - {title}", ((string, byte[]))files[2]);
        }

        // Collections lazer : une existe déjà dans stable (fusion), les autres sont nouvelles.
        void AddCollection(string name, IEnumerable<string> md5s)
        {
            var c = realm.DynamicApi.CreateObject("BeatmapCollection", Guid.NewGuid());
            c.DynamicApi.Set("Name", name);
            c.DynamicApi.Set("LastModified", DateTimeOffset.UtcNow);
            var list = c.DynamicApi.GetList<string>("BeatmapMD5Hashes");
            foreach (string h in md5s) list.Add(h);
        }

        if (showcaseMd5.Count > 0)
        {
            AddCollection("Favoris", showcaseMd5.Take(12).SelectMany(x => x));
            AddCollection("Farm 300pp", showcaseMd5.Skip(10).Take(15).SelectMany(x => x.Take(1)));
            AddCollection("Tournoi d'hiver", showcaseMd5.Skip(30).Take(8).SelectMany(x => x));
            AddCollection("Stream practice", showcaseMd5.Skip(40).Take(6).SelectMany(x => x));

            // stable : installation avec osu!.exe et un collection.db qui contient déjà « Favoris » (4 maps).
            string stableDir = Path.GetDirectoryName(songs)!;
            File.WriteAllBytes(Path.Combine(stableDir, "osu!.exe"), [0x4D, 0x5A]);
            using var w = new BinaryWriter(File.Create(Path.Combine(stableDir, "collection.db")));
            w.Write(20240820);
            w.Write(2);
            void S(string v) { var bytes = Encoding.UTF8.GetBytes(v); w.Write((byte)0x0B); w.Write((byte)bytes.Length); w.Write(bytes); }
            S("Favoris");
            var fav = showcaseMd5.Take(4).SelectMany(x => x.Take(1)).ToList();
            w.Write(fav.Count);
            foreach (string h in fav) S(h);
            S("Vieilles maps");
            w.Write(1);
            S("0123456789abcdef0123456789abcdef");
            expected.Add("collections : 4 dans lazer, Favoris complétée, 3 ajoutées");

            // ---------------------------------------------------------- scores
            // Joueur fictif « nightjar », connecté avec ce pseudo dans lazer et dans stable.
            File.WriteAllText(Path.Combine(lazerDir, "game.ini"), "Username = nightjar\n");
            File.WriteAllText(Path.Combine(stableDir, "osu!.nightjar.cfg"), "BeatmapDirectory = Songs\nUsername = nightjar\n");
            string dataR = Path.Combine(stableDir, "Data", "r");
            Directory.CreateDirectory(dataR);
            var inStable = new List<OsuScore>();
            int scoreNo = 0;

            IRealmObject FirstBeatmap(IRealmObject set) => set.DynamicApi.GetList<IRealmObject>("Beatmaps")[0];

            void AddScore(IRealmObject beatmap, string player, bool legacy, string mods, int legacyMods, double acc, int objects, int combo,
                int ageDays, bool stableHasIt = false, bool dropReplay = false, string ruleset = "osu")
            {
                int n = ++scoreNo;
                int miss = acc > 0.995 ? 0 : (int)Math.Round(objects * (1 - acc) / 6);
                int n100 = Math.Max(0, (int)Math.Round(objects * (1 - acc) * 1.2) - miss);
                int n50 = acc < 0.93 ? 1 + n % 3 : 0;
                int n300 = Math.Max(0, objects - n100 - n50 - miss);
                var date = DateTimeOffset.UtcNow.AddDays(-ageDays).AddMinutes(-n * 37);
                long standardised = (long)Math.Round(1_000_000 * Math.Pow(acc, 5) * (0.55 + 0.45 * combo / (double)objects));
                long legacyTotal = (long)Math.Round(Math.Pow(objects, 2) * 31 * acc * combo / objects + 50_000);
                string md5 = beatmap.DynamicApi.Get<string>("MD5Hash");

                var osr = new OsuScore
                {
                    Mode = 0,
                    Version = legacy ? 20240820 : OsuScore.FirstLazerVersion + 19,
                    BeatmapMd5 = md5,
                    Player = player,
                    ReplayMd5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"replay-{n}"))).ToLowerInvariant(),
                    Count300 = (ushort)n300, Count100 = (ushort)n100, Count50 = (ushort)n50, CountMiss = (ushort)miss,
                    TotalScore = (int)(legacy ? legacyTotal : standardised),
                    MaxCombo = (ushort)combo,
                    Perfect = miss == 0 && combo >= objects,
                    Mods = legacyMods,
                    LifeBar = "",
                    Ticks = date.UtcTicks,
                    ReplayData = Bin("frames" + n, 1800 + n * 13),
                    OnlineId = legacy ? 4_100_000_000 + n : 0,
                };
                byte[] bytes = osr.ToReplayBytes();
                if (!legacy) bytes = [.. bytes, .. BitConverter.GetBytes(64), .. Bin("lazer-json" + n)]; // données propres à lazer, à ignorer
                var file = AddFile(bytes);

                var s = (IRealmObject)realm.DynamicApi.CreateObject("Score", Guid.NewGuid());
                s.DynamicApi.Set("BeatmapInfo", RealmValue.Object(beatmap));
                s.DynamicApi.Set("BeatmapHash", beatmap.DynamicApi.Get<string>("Hash"));
                s.DynamicApi.Set("Ruleset", RealmValue.Object(rulesets[ruleset]));
                s.DynamicApi.Set("TotalScore", standardised);
                s.DynamicApi.Set("TotalScoreVersion", 30000019);
                if (legacy) s.DynamicApi.Set("LegacyTotalScore", legacyTotal);
                s.DynamicApi.Set("MaxCombo", combo);
                s.DynamicApi.Set("Accuracy", acc);
                s.DynamicApi.Set("Date", date);
                s.DynamicApi.Set("IsLegacyScore", legacy);
                s.DynamicApi.Set("LegacyOnlineID", legacy ? osr.OnlineId : -1);
                s.DynamicApi.Set("Mods", mods);
                s.DynamicApi.Set("Statistics", $"{{\"great\":{n300},\"ok\":{n100},\"meh\":{n50},\"miss\":{miss}}}");
                s.DynamicApi.Set("MaximumStatistics", $"{{\"great\":{objects}}}");
                s.DynamicApi.Set("Rank", acc > 0.99 ? 4 : 3);
                var user = realm.DynamicApi.CreateEmbeddedObjectForProperty(s, "User");
                user.DynamicApi.Set("Username", player);
                user.DynamicApi.Set("OnlineID", player == "Guest" ? 0 : 1000 + player.Length);
                var usage = realm.DynamicApi.AddEmbeddedObjectToList(s.DynamicApi.GetList<IEmbeddedObject>("Files"));
                usage.DynamicApi.Set("File", RealmValue.Object(file));
                usage.DynamicApi.Set("Filename", $"{player} - replay {n}.osr");

                if (stableHasIt)
                {
                    File.WriteAllBytes(Path.Combine(dataR, osr.ReplayFileName), bytes);
                    osr.ReplayData = null;
                    inStable.Add(osr);
                }

                if (dropReplay)
                {
                    string h = file.DynamicApi.Get<string>("Hash");
                    File.Delete(Path.Combine(lazerDir, "files", h[..1], h[..2], h));
                }
            }

            string[] modSets = ["[]", "[{\"acronym\":\"HD\"}]", "[{\"acronym\":\"HD\"},{\"acronym\":\"DT\"}]", "[{\"acronym\":\"HR\"}]",
                "[{\"acronym\":\"HD\"},{\"acronym\":\"HR\"}]", "[{\"acronym\":\"DT\"}]", "[{\"acronym\":\"NF\"}]"];
            int[] legacyModSets = [0, 8, 8 | 64, 16, 8 | 16, 64, 1];
            double[] accs = [0.9934, 0.9812, 1.0, 0.9655, 0.9871, 0.9433, 0.9978, 0.9216, 0.9759, 0.9902, 0.9581, 0.9999, 0.9690, 0.9847];

            // Joués dans lazer sur des maps déjà dans stable (dossier « i % 3 == 2 ») : à copier.
            int k = 0;
            for (int i = 2; i < showcaseSets.Count && k < 14; i += 3, k++)
            {
                int objects = 180 + i * 11;
                AddScore(FirstBeatmap(showcaseSets[i]), "nightjar", false, modSets[k % modSets.Length], legacyModSets[k % modSets.Length],
                    accs[k % accs.Length], objects, k % 4 == 0 ? objects : objects / 2 + k * 7, ageDays: k);
            }

            // Joués dans lazer sur des maps qui vont être copiées : ils suivent leur map.
            for (int i = 0, j = 0; j < 6; i += 3, j++)
                AddScore(FirstBeatmap(showcaseSets[i]), "nightjar", false, modSets[(j + 2) % modSets.Length], legacyModSets[(j + 2) % modSets.Length],
                    accs[(j + 5) % accs.Length], 240 + j * 20, 200 + j * 15, ageDays: 2 + j);

            // Joués en invité dans lazer.
            for (int j = 0; j < 3; j++)
                AddScore(FirstBeatmap(showcaseSets[44 + j * 3 + 2 - (44 % 3)]), "Guest", false, "[]", 0, accs[(j + 9) % accs.Length], 300, 250, ageDays: 5 + j);

            // Venus de stable et déjà dans son classement Local.
            for (int j = 0; j < 8; j++)
                AddScore(FirstBeatmap(showcaseSets[2 + j * 3]), "nightjar", true, "[{\"acronym\":\"CL\"}]", 0, accs[(j + 3) % accs.Length], 260, 200,
                    ageDays: 40 + j * 9, stableHasIt: true);

            // Replays d'autres joueurs téléchargés dans lazer.
            string[] others = ["aoi_rin", "Mira", "Tenshi", "kuroneko"];
            for (int j = 0; j < others.Length; j++)
                AddScore(FirstBeatmap(showcaseSets[5 + j * 3]), others[j], true, "[{\"acronym\":\"HD\"},{\"acronym\":\"DT\"},{\"acronym\":\"CL\"}]", 8 | 64,
                    0.99 - j * 0.004, 420, 420, ageDays: 300 + j * 40);

            // Cas à ignorer.
            AddScore(FirstBeatmap(showcaseSets[8]), "nightjar", false, "[{\"acronym\":\"DA\",\"settings\":{\"circle_size\":3}}]", 0, 0.97, 200, 150, ageDays: 1);
            AddScore(FirstBeatmap(showcaseSets[11]), "nightjar", false, "[{\"acronym\":\"DT\",\"settings\":{\"speed_change\":1.3}}]", 64, 0.95, 200, 150, ageDays: 1);
            AddScore(FirstBeatmap(showcaseSets[14]), "nightjar", false, "[]", 0, 0.96, 200, 180, ageDays: 3, dropReplay: true);
            AddScore(FirstBeatmap(setF), "nightjar", false, "[]", 0, 0.99, 120, 120, ageDays: 4);
            AddScore(FirstBeatmap(setI), "nightjar", false, "[]", 0, 0.99, 120, 120, ageDays: 4, ruleset: "tau");

            // scores.db de stable : les scores « déjà là », plus un score d'une map sans rapport.
            using (var sw = new BinaryWriter(File.Create(Path.Combine(stableDir, "scores.db"))))
            {
                var groups = inStable.GroupBy(x => x.BeatmapMd5).ToList();
                sw.Write(20260924);
                sw.Write(groups.Count + 1);
                foreach (var g in groups)
                {
                    OsuBinary.WriteString(sw, g.Key);
                    sw.Write(g.Count());
                    foreach (var x in g) x.WriteDbEntry(sw);
                }

                var unrelated = new OsuScore
                {
                    Version = 20240820, BeatmapMd5 = "0123456789abcdef0123456789abcdef", Player = "nightjar", ReplayMd5 = "fedcba9876543210fedcba9876543210",
                    Count300 = 100, TotalScore = 123456, MaxCombo = 150, Ticks = DateTime.UtcNow.AddYears(-2).Ticks, LifeBar = "", OnlineId = 7,
                };
                OsuBinary.WriteString(sw, unrelated.BeatmapMd5);
                sw.Write(1);
                unrelated.WriteDbEntry(sw);
            }

            expected.Add($"scores : {scoreNo} dans lazer ({inStable.Count} déjà dans stable)");
        }

        // ------------------------------------------------------------- volume

        int bulkPresent = 0, bulkMissing = 0;
        for (int i = 0; i < bulk; i++)
        {
            int setId = 200000 + i;
            int diffs = 1 + rnd.Next(4);
            var files = new List<(string, byte[])> { ("audio.mp3", Bin("bulk" + i, 2000)), ("bg.png", Bin("bulkbg" + i, 500)) };
            var d = new List<(string, int, string, string)>();
            for (int k = 0; k < diffs; k++)
            {
                string fn = $"Bulk Artist {i} - Bulk Title {i} (bulker) [Diff {k}].osu";
                files.Add((fn, Osu(14, $"Diff {k}", setId * 10 + k, setId)));
                d.Add(($"Diff {k}", setId * 10 + k, fn, "osu"));
            }

            MakeSet(setId, $"Bulk Artist {i}", $"Bulk Title {i} — ünïcödé ♪", "bulker", files.ToArray(), d.ToArray());

            if (i % 2 == 0)
            {
                StableFolder($"{setId} Bulk Artist {i} - Bulk Title {i}", ((string, byte[]))files[2]);
                bulkPresent++;
            }
            else bulkMissing++;
        }

        expected.Add($"bulk : {bulkPresent} present, {bulkMissing} copy");

        // Quelques scores, sans rapport
        for (int i = 0; i < 10; i++)
        {
            var s = realm.DynamicApi.CreateObject("Score", Guid.NewGuid());
            s.DynamicApi.Set("TotalScore", 1000 * i);
        }
    });
}

File.WriteAllLines(Path.Combine(root, "expected.txt"), expected);
Console.WriteLine($"Généré dans {root}");
foreach (string e in expected) Console.WriteLine("  " + e);

// ---------------------------------------------------------------------- fonctions

static byte[] Bin(string seed, int size = 64)
{
    var r = new Random(seed.GetHashCode(StringComparison.Ordinal) ^ size);
    var b = new byte[size];
    r.NextBytes(b);
    Encoding.ASCII.GetBytes(seed).CopyTo(b, 0);
    return b;
}

static byte[] Osu(int version, string diff, int beatmapId, int setId) => Encoding.UTF8.GetBytes(
    $"osu file format v{version}\r\n\r\n[General]\r\nAudioFilename: audio.mp3\r\n\r\n[Metadata]\r\nTitle:T\r\nVersion:{diff}\r\n" +
    (beatmapId > 0 || version >= 10 ? $"BeatmapID:{beatmapId}\r\nBeatmapSetID:{setId}\r\n" : "") +
    "\r\n[Difficulty]\r\nCircleSize:4\r\n\r\n[HitObjects]\r\n256,192,1000,1,0\r\n");

void StableFolder(string name, params (string file, byte[] content)[] files)
{
    string dir = Path.Combine(songs, name);
    Directory.CreateDirectory(dir);
    foreach (var (file, content) in files)
        File.WriteAllBytes(Path.Combine(dir, file), content);
}
