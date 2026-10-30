using System.Globalization;
using System.Text.Json;

namespace Exotique.Core;

public static class Fmt
{
    private static Lang L => Loc.I.Lang;

    /// <summary>Nombre avec séparateur de milliers : 12 345 (fr, ru, espace insécable U+00A0) ou 12,345 (en).</summary>
    public static string N(long n) =>
        n.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', L == Lang.En ? ',' : '\u00A0');

    public static string Bytes(long bytes)
    {
        string[] units = L switch
        {
            Lang.En => ["B", "KB", "MB", "GB", "TB"],
            Lang.Ru => ["Б", "КБ", "МБ", "ГБ", "ТБ"],
            _ => ["o", "Ko", "Mo", "Go", "To"],
        };

        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1)
        {
            v /= 1024;
            u++;
        }

        string num = u <= 1 ? v.ToString("0", CultureInfo.InvariantCulture) : v.ToString("0.0", CultureInfo.InvariantCulture);
        if (L != Lang.En) num = num.Replace('.', ',');
        return num + "\u00A0" + units[u];
    }

    /// <summary>Précision : 98,64 % (fr), 98.64% (en), 98,64% (ru).</summary>
    public static string Percent(double fraction)
    {
        // Tronquée comme dans osu! : 99,999 % ne doit pas s'afficher 100,00 %.
        double v = Math.Floor(fraction * 10000 + 1e-7) / 100;
        string num = v.ToString("0.00", CultureInfo.InvariantCulture);
        return L switch
        {
            Lang.En => num + "%",
            Lang.Ru => num.Replace('.', ',') + "%",
            _ => num.Replace('.', ',') + " %",
        };
    }

    public static string Duration(TimeSpan t)
    {
        (string s, string min, string h) = L == Lang.Ru ? ("с", "мин", "ч") : ("s", "min", "h");
        if (t.TotalSeconds < 60) return $"{Math.Max(1, (int)t.TotalSeconds)} {s}";
        if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes} {min} {t.Seconds:00} {s}";
        return $"{(int)t.TotalHours} {h} {t.Minutes:00} {min}";
    }

    public static long? FreeSpace(string path)
    {
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return null;
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Réglages mémorisés entre deux lancements (%APPDATA%\Exotique\settings.json).</summary>
public sealed class Settings
{
    public string? LazerPath { get; set; }
    public string? StablePath { get; set; }
    public bool ExportAsOsz { get; set; }
    public bool ExportCollections { get; set; } = true;
    public bool CopyBeatmaps { get; set; } = true;
    public bool CopyScores { get; set; } = true;
    public bool CopySkins { get; set; } = true;
    public bool ScoresIncludeOthers { get; set; }

    /// <summary>« fr », « en » ou « ru » ; vide : langue de Windows.</summary>
    public string? Language { get; set; }

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Exotique", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            // fichier abîmé : réglages par défaut
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // non bloquant
        }
    }
}
