using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Exotique.Core;

public enum Lang { Fr, En, Ru }

/// <summary>
/// Traductions : <c>Loc.T("Clé")</c>, <c>Loc.F("Clé", args)</c> pour un texte à trous,
/// <c>Loc.P("Clé", n)</c> pour un pluriel (formes séparées par « | », 3 formes en russe).
/// L'indexeur sert aux liaisons XAML et se met à jour quand la langue change.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc I { get; } = new();

    private Lang lang = Detect();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Après un changement de langue, pour recalculer les textes construits en code.</summary>
    public event Action? LanguageChanged;

    public Lang Lang
    {
        get => lang;
        set
        {
            if (lang == value) return;
            lang = value;
            // Les liaisons d'indexeur écoutent « Item » ou « Item[] » selon le framework : on envoie les deux.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Lang)));
            LanguageChanged?.Invoke();
        }
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (!Strings.Table.TryGetValue(key, out var t)) return key;
        return lang switch
        {
            Lang.En => t.En,
            Lang.Ru => t.Ru,
            _ => t.Fr,
        };
    }

    public static string T(string key) => I.Get(key);

    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(key), args);

    public static string P(string key, long n)
    {
        string[] forms = T(key).Split('|');
        return forms[PluralIndex(n, forms.Length)].Replace("{n}", Fmt.N(n), StringComparison.Ordinal);
    }

    private static int PluralIndex(long n, int forms)
    {
        if (forms <= 1) return 0;
        long a = Math.Abs(n);

        switch (I.lang)
        {
            case Lang.Ru when forms >= 3:
                if (a % 10 == 1 && a % 100 != 11) return 0;
                if (a % 10 is >= 2 and <= 4 && a % 100 is < 12 or > 14) return 1;
                return 2;
            case Lang.Fr:
                return a <= 1 ? 0 : 1;
            default:
                return a == 1 ? 0 : 1;
        }
    }

    /// <summary>« a, b et c » / « a, b and c » / « a, b и c ».</summary>
    public static string JoinAnd(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => "",
        1 => parts[0],
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} {T("And")} {parts[^1]}",
    };

    public static string Code(Lang l) => l switch { Lang.En => "en", Lang.Ru => "ru", _ => "fr" };

    public static Lang? Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "fr" => Lang.Fr,
        "en" => Lang.En,
        "ru" => Lang.Ru,
        _ => null,
    };

    /// <summary>Langue de Windows (ou EXOTIQUE_LANG / LANG ailleurs) ; anglais par défaut.</summary>
    public static Lang Detect()
    {
        if (Parse(Environment.GetEnvironmentVariable("EXOTIQUE_LANG")) is Lang forced) return forced;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                int primary = GetUserDefaultUILanguage() & 0x3FF;
                return primary switch
                {
                    0x0C => Lang.Fr,
                    0x19 => Lang.Ru,
                    _ => Lang.En,
                };
            }
            catch
            {
                return Lang.En;
            }
        }

        string env = Environment.GetEnvironmentVariable("LANG") ?? "";
        return env.StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? Lang.Fr
            : env.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? Lang.Ru
            : Lang.En;
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
}
