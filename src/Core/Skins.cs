using System.Security.Cryptography;
using System.Text;
using Realms;

namespace Exotique.Core;

/// <summary>Une skin de la base lazer.</summary>
public sealed class LazerSkin
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Creator { get; init; } = "";
    public string Instantiation { get; init; } = "";
    public List<LazerFile> Files { get; } = [];

    /// <summary>Skin au format classique (importée de stable ou d'un .osk) : stable sait la lire telle quelle.</summary>
    public bool IsClassic => Instantiation.Contains("LegacySkin", StringComparison.Ordinal)
                             && !Instantiation.Contains("DefaultLegacySkin", StringComparison.Ordinal);

    /// <summary>
    /// lazer nomme les skins importées « Nom [dossier d'origine] » quand les deux diffèrent :
    /// le dossier d'origine est celui que stable avait (ou le nom du .osk).
    /// </summary>
    public string? OriginalFolder
    {
        get
        {
            if (!Name.EndsWith(']')) return null;
            int open = Name.LastIndexOf(" [", StringComparison.Ordinal);
            if (open < 0) return null;
            string inner = Name[(open + 2)..^1].Trim();
            return inner.Length > 0 ? inner : null;
        }
    }

    /// <summary>Nom à afficher, sans le dossier d'origine entre crochets.</summary>
    public string DisplayName
    {
        get
        {
            int open = OriginalFolder == null ? -1 : Name.LastIndexOf(" [", StringComparison.Ordinal);
            return open > 0 ? Name[..open].Trim() : Name.Trim();
        }
    }

    public LazerFile? FindFile(string name) =>
        Files.FirstOrDefault(f => f.Filename.Replace('\\', '/').Equals(name, StringComparison.OrdinalIgnoreCase));
}

internal static class LazerSkinReader
{
    public static List<LazerSkin> Read(Realm realm)
    {
        var result = new List<LazerSkin>();
        IQueryable<IRealmObject> all;
        try
        {
            all = realm.DynamicApi.All("Skin");
        }
        catch
        {
            return result;
        }

        foreach (var s in all)
        {
            try
            {
                if (Get<bool>(s, "Protected") || Get<bool>(s, "DeletePending")) continue;

                var skin = new LazerSkin
                {
                    Id = Get<Guid>(s, "ID"),
                    Name = Get<string?>(s, "Name") ?? "",
                    Creator = Get<string?>(s, "Creator") ?? "",
                    Instantiation = Get<string?>(s, "InstantiationInfo") ?? "",
                };

                foreach (var usage in s.DynamicApi.GetList<IEmbeddedObject>("Files"))
                {
                    var file = Get<IRealmObject?>(usage, "File");
                    string? name = Get<string?>(usage, "Filename");
                    string? hash = file == null ? null : Get<string?>(file, "Hash");
                    if (string.IsNullOrEmpty(name) || hash is not { Length: > 2 }) continue;
                    skin.Files.Add(new LazerFile(name, hash.ToLowerInvariant()));
                }

                if (skin.Files.Count > 0 || !skin.IsClassic) result.Add(skin);
            }
            catch
            {
                // skin illisible : on passe
            }
        }

        return result;
    }

    private static T Get<T>(IRealmObjectBase o, string prop, T fallback = default!)
    {
        try { return o.DynamicApi.Get<T>(prop); }
        catch { return fallback; }
    }
}

/// <summary>Images de l'aperçu d'une skin : un cercle avec son chiffre, et le curseur.</summary>
public sealed record SkinPreview(string? Circle, string? Overlay, string? Number, string? Cursor, (byte R, byte G, byte B) Combo);

public sealed class SkinPlanItem
{
    public required LazerSkin Skin { get; init; }
    public required ScoreDecision Decision { get; init; }
    public string? ReasonKey { get; init; }

    /// <summary>Dossier à créer dans Skins.</summary>
    public string FolderName { get; init; } = "";

    public long Bytes { get; init; }
    public SkinPreview? Preview { get; init; }

    public string Reason => ReasonKey == null ? "" : Loc.T(ReasonKey);
}

public sealed class SkinPlan
{
    public List<SkinPlanItem> Items { get; init; } = [];
    public string? Unavailable { get; init; }
    public string? SkinsDir { get; init; }
}

public static class SkinPlanner
{
    public static SkinPlan Build(IReadOnlyList<LazerSkin> skins, LazerLocation lazer, StableLocation stable)
    {
        if (stable.InstallDir == null) return new SkinPlan { Unavailable = Loc.T("SkinsNoInstall") };

        string skinsDir = Path.Combine(stable.InstallDir, "Skins");
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var iniHashes = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (Directory.Exists(skinsDir))
            {
                foreach (string dir in Directory.EnumerateDirectories(skinsDir))
                {
                    folders.Add(Path.GetFileName(dir));
                    string? ini = Directory.EnumerateFiles(dir).FirstOrDefault(f => Path.GetFileName(f).Equals("skin.ini", StringComparison.OrdinalIgnoreCase));
                    if (ini != null) iniHashes.Add(HashFile(ini));
                }
            }
        }
        catch
        {
            // dossier Skins illisible : on se fie aux noms trouvés
        }

        var items = new List<SkinPlanItem>();
        var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var skin in skins.OrderByDescending(s => s.Files.Count))
        {
            if (!skin.IsClassic)
            {
                items.Add(new SkinPlanItem { Skin = skin, Decision = ScoreDecision.Ignored, ReasonKey = "ReasonSkinLazer" });
                continue;
            }

            var ini = skin.FindFile("skin.ini");
            string? iniPath = ini == null ? null : lazer.PathForHash(ini.Hash);
            var preview = BuildPreview(skin, lazer, iniPath);
            long bytes = skin.Files.Sum(f => SizeOf(lazer.PathForHash(f.Hash)));

            string folder = Exporter.SanitizeSegment(skin.OriginalFolder ?? skin.DisplayName);
            if (folder.Length > 120) folder = folder[..120].TrimEnd(' ', '.');
            if (folder.Length == 0) folder = skin.Id.ToString();

            // Déjà dans stable : même dossier (d'origine, nom de la skin, ou nom d'un export lazer « Nom (Auteur) »), ou même skin.ini.
            string[] candidates = [folder, skin.DisplayName, $"{skin.DisplayName} ({skin.Creator})"];
            bool present = candidates.Any(c => folders.Contains(c.Trim()))
                           || (iniPath != null && File.Exists(iniPath) && iniHashes.Contains(HashFile(iniPath)));

            if (present)
                items.Add(new SkinPlanItem { Skin = skin, Decision = ScoreDecision.AlreadyPresent, ReasonKey = "ReasonSkinInStable", FolderName = folder, Bytes = bytes, Preview = preview });
            else if (!planned.Add(folder))
                items.Add(new SkinPlanItem { Skin = skin, Decision = ScoreDecision.AlreadyPresent, ReasonKey = "ReasonSkinDuplicate", FolderName = folder, Bytes = bytes, Preview = preview });
            else
                items.Add(new SkinPlanItem { Skin = skin, Decision = ScoreDecision.Copy, FolderName = folder, Bytes = bytes, Preview = preview });
        }

        return new SkinPlan { Items = items, SkinsDir = skinsDir };
    }

    private static SkinPreview BuildPreview(LazerSkin skin, LazerLocation lazer, string? iniPath)
    {
        string prefix = "default";
        (byte, byte, byte) combo = (255, 192, 0); // couleur du premier combo de la skin par défaut

        if (iniPath != null && File.Exists(iniPath))
        {
            try
            {
                string section = "";
                foreach (string raw in File.ReadLines(iniPath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.StartsWith('[')) { section = line; continue; }
                    int colon = line.IndexOf(':');
                    if (colon <= 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;
                    string key = line[..colon].Trim(), value = line[(colon + 1)..].Trim();

                    if (section.Equals("[Fonts]", StringComparison.OrdinalIgnoreCase) && key.Equals("HitCirclePrefix", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                        prefix = value.Replace('\\', '/');
                    else if (section.Equals("[Colours]", StringComparison.OrdinalIgnoreCase) && key.Equals("Combo1", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] p = value.Split(',');
                        if (p.Length >= 3 && byte.TryParse(p[0].Trim(), out byte r) && byte.TryParse(p[1].Trim(), out byte g) && byte.TryParse(p[2].Trim(), out byte b))
                            combo = (r, g, b);
                    }
                }
            }
            catch
            {
                // skin.ini illisible : valeurs par défaut
            }
        }

        string? Find(string name)
        {
            var f = skin.FindFile(name + "@2x.png") ?? skin.FindFile(name + ".png");
            return f == null ? null : lazer.PathForHash(f.Hash);
        }

        return new SkinPreview(Find("hitcircle"), Find("hitcircleoverlay"), Find(prefix + "-1"), Find("cursor"), combo);
    }

    private static long SizeOf(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    private static string HashFile(string path)
    {
        try { return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))); }
        catch { return ""; }
    }
}

public sealed class SkinExportResult
{
    public List<(SkinPlanItem Item, string Destination)> Exported { get; } = [];
    public List<(SkinPlanItem Item, string Error)> Failed { get; } = [];
    public long BytesCopied { get; set; }
}

/// <summary>Copie les skins choisies dans le dossier Skins de stable, une par dossier.</summary>
public static class SkinExporter
{
    public static SkinExportResult Run(IReadOnlyList<SkinPlanItem> items, LazerLocation lazer, string skinsDir, IProgress<(int Done, int Total, SkinPlanItem? Current)>? progress = null)
    {
        var result = new SkinExportResult();
        string tempRoot = Path.Combine(skinsDir, StableIndex.TempFolderName);
        Exporter.DeleteQuietly(tempRoot);
        Directory.CreateDirectory(tempRoot);

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                progress?.Report((i, items.Count, item));
                try
                {
                    string name = Exporter.UniqueName(item.FolderName, n => Directory.Exists(Path.Combine(skinsDir, n)));
                    string temp = Path.Combine(tempRoot, name);
                    Directory.CreateDirectory(temp);

                    foreach (var file in item.Skin.Files)
                    {
                        string? rel = Exporter.SafeRelativePath(file.Filename);
                        string src = lazer.PathForHash(file.Hash);
                        if (rel == null || !File.Exists(src)) continue;

                        string dst = Path.Combine(temp, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                        File.Copy(src, dst, overwrite: true);
                        result.BytesCopied += new FileInfo(src).Length;
                    }

                    string final = Path.Combine(skinsDir, name);
                    Directory.Move(temp, final);
                    result.Exported.Add((item, final));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    result.Failed.Add((item, e.Message));
                }
            }
        }
        finally
        {
            Exporter.DeleteQuietly(tempRoot);
        }

        progress?.Report((items.Count, items.Count, null));
        return result;
    }
}
