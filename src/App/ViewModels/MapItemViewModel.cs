using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Exotique.Core;

namespace Exotique.App.ViewModels;

public sealed record DifficultyDot(IBrush Fill, IBrush Stroke, string Tip);

public sealed partial class MapItemViewModel : ObservableObject
{
    private readonly LazerLocation lazer;
    private Bitmap? thumbnail;
    private bool thumbnailRequested;

    public MapItemViewModel(PlanItem item, LazerLocation lazer)
    {
        Item = item;
        this.lazer = lazer;
        isSelected = item.Decision == Decision.Copy;

        var set = item.Set;
        title = set.Title;
        Artist = set.Artist;
        Creator = set.Creator;

        var stable = set.Beatmaps.OrderBy(b => b.StarRating).ToList();
        Difficulties = stable.Take(MaxDots).Select(b => StarColors.Dot(b)).ToList();
        ExtraDifficulties = stable.Count > MaxDots ? $"+{stable.Count - MaxDots}" : "";
        DifficultyCount = stable.Count;

        Modes = string.Join("  ", set.Beatmaps.Select(b => b.RulesetId).Distinct().OrderBy(m => m < 0 ? 99 : m).Select(ModeName));
        (statusKey, StatusBrush) = StatusOf(set);

        SearchText = string.Join('\n', new[] { set.Title, set.Artist, set.TitleUnicode, set.ArtistUnicode, set.Creator }
            .Concat(set.Beatmaps.Select(b => b.DifficultyName))
            .Append(set.OnlineId > 0 ? set.OnlineId.ToString() : "")).ToLowerInvariant();

        MaxStars = set.Beatmaps.Count == 0 ? 0 : set.Beatmaps.Max(b => b.StarRating);
    }

    private const int MaxDots = 14;

    public PlanItem Item { get; }
    public Decision Decision => Item.Decision;
    public bool IsCopyCandidate => Item.Decision == Decision.Copy;
    public bool IsPresent => Item.Decision == Decision.AlreadyPresent;
    public bool IsIgnored => Item.Decision is Decision.LazerEdited or Decision.UnsupportedRuleset or Decision.MissingFiles;

    private readonly string title;
    private readonly string statusKey;

    public string Title => title.Length > 0 ? title : Loc.T("NoTitle");
    public string Artist { get; }
    public string Creator { get; }
    public string ArtistLine => Creator.Length > 0 ? Loc.F("MappedBy", Artist, Creator) : Artist;
    public string Modes { get; }
    public string StatusText => statusKey.Length > 0 ? Loc.T(statusKey) : "";
    public IBrush StatusBrush { get; }
    public bool HasStatus => statusKey.Length > 0;
    public IReadOnlyList<DifficultyDot> Difficulties { get; }
    public string ExtraDifficulties { get; }
    public int DifficultyCount { get; }
    public double MaxStars { get; }
    public string SizeText => Fmt.Bytes(Item.Bytes);
    /// <summary>Pourquoi la map est déjà dans stable, ou pourquoi elle est ignorée (dans la langue en cours).</summary>
    public string ReasonText => Item.Reason;

    /// <summary>Après un changement de langue.</summary>
    internal void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ArtistLine));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ReasonText));
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(FullName));
    }
    public string SearchText { get; }
    public DateTimeOffset DateAdded => Item.Set.DateAdded;

    [ObservableProperty]
    private bool isSelected;

    /// <summary>Raccourci affiché pendant la copie (« 2134656 Ludicin - Clinozoisite »).</summary>
    public string FullName => $"{Artist} - {Title}";

    // ------------------------------------------------------------- vignette

    /// <summary>Chargée à la demande : seules les lignes visibles de la liste la demandent.</summary>
    public Bitmap? Thumbnail
    {
        get
        {
            if (thumbnail == null && !thumbnailRequested)
            {
                thumbnailRequested = true;
                string? hash = Item.Set.BackgroundHash();
                if (hash != null)
                    ThumbnailLoader.Request(this, lazer.PathForHash(hash));
            }

            return thumbnail;
        }
    }

    internal void SetThumbnail(Bitmap? bitmap)
    {
        thumbnail = bitmap;
        OnPropertyChanged(nameof(Thumbnail));
    }

    /// <summary>Libère la vignette (cache plein) ; elle sera rechargée si la ligne redevient visible.</summary>
    internal void ReleaseThumbnail()
    {
        var old = thumbnail;
        thumbnail = null;
        thumbnailRequested = false;
        OnPropertyChanged(nameof(Thumbnail));
        old?.Dispose();
    }

    // -------------------------------------------------------------- helpers

    public static string ModeName(int ruleset) => ruleset switch
    {
        0 => "osu!",
        1 => "taiko",
        2 => "catch",
        3 => "mania",
        _ => "custom",
    };

    private static (string Key, IBrush Brush) StatusOf(LazerSet set)
    {
        if (set.OnlineId <= 0) return ("StatusLocal", Palette.Muted);

        return set.Status switch
        {
            1 => ("StatusRanked", Palette.Ranked),
            2 => ("StatusApproved", Palette.Ranked),
            3 => ("StatusQualified", Palette.Stable),
            4 => ("StatusLoved", Palette.Lazer),
            0 => ("StatusPending", Palette.Warn),
            -1 => ("StatusWip", Palette.Warn),
            -2 => ("StatusGraveyard", Palette.Muted),
            _ => ("", Palette.Muted),
        };
    }
}

/// <summary>Couleurs de difficulté façon osu! (dégradé du site officiel selon les étoiles).</summary>
public static class StarColors
{
    private static readonly double[] Stops = [0.1, 1.25, 2.0, 2.5, 3.3, 4.2, 4.9, 5.8, 6.7, 7.7, 9.0];

    private static readonly Color[] Colors =
    [
        Color.Parse("#4290FB"), Color.Parse("#4FC0FF"), Color.Parse("#4FFFD5"), Color.Parse("#7CFF4F"),
        Color.Parse("#F6F05C"), Color.Parse("#FF8068"), Color.Parse("#FF4E6F"), Color.Parse("#C645B8"),
        Color.Parse("#6563DE"), Color.Parse("#18158E"), Color.Parse("#000000"),
    ];

    public static Color ForStars(double stars)
    {
        if (stars < 0) return Color.Parse("#8A84A8");
        if (stars <= Stops[0]) return Colors[0];
        if (stars >= Stops[^1]) return Colors[^1];

        for (int i = 1; i < Stops.Length; i++)
        {
            if (stars > Stops[i]) continue;
            double t = (stars - Stops[i - 1]) / (Stops[i] - Stops[i - 1]);
            var a = Colors[i - 1];
            var b = Colors[i];
            return Color.FromRgb(Lerp(a.R, b.R, t), Lerp(a.G, b.G, t), Lerp(a.B, b.B, t));
        }

        return Colors[^1];

        static byte Lerp(byte x, byte y, double t) => (byte)Math.Round(x + (y - x) * t);
    }

    public static DifficultyDot Dot(LazerBeatmap b)
    {
        var color = ForStars(b.StarRating);
        // Les difficultés très hautes sont presque noires : un liseré clair les garde visibles.
        IBrush stroke = b.StarRating >= 6.5 ? Palette.Line : Brushes.Transparent;
        string stars = b.StarRating >= 0
            ? (Loc.I.Lang == Lang.En ? $"{b.StarRating:0.00}★" : $"{b.StarRating:0.00}★".Replace('.', ','))
            : Loc.T("StarsUnknown");
        return new DifficultyDot(new SolidColorBrush(color), stroke, $"{b.DifficultyName}  {stars}  {MapItemViewModel.ModeName(b.RulesetId)}");
    }
}

public static class Palette
{
    public static readonly IBrush Lazer = new SolidColorBrush(Color.Parse("#FF62A8"));
    public static readonly IBrush Stable = new SolidColorBrush(Color.Parse("#63C9FF"));
    public static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#A39BC9"));
    public static readonly IBrush Line = new SolidColorBrush(Color.Parse("#4A3F78"));
    public static readonly IBrush Warn = new SolidColorBrush(Color.Parse("#FFC363"));
    public static readonly IBrush Ranked = new SolidColorBrush(Color.Parse("#B3FF66"));

    // Couleurs des rangs, comme les lettres de stable.
    public static readonly IBrush GradeGold = new SolidColorBrush(Color.Parse("#FFCF4D"));
    public static readonly IBrush GradeSilver = new SolidColorBrush(Color.Parse("#DCE3F0"));
    public static readonly IBrush GradeA = new SolidColorBrush(Color.Parse("#8CE65A"));
    public static readonly IBrush GradeB = new SolidColorBrush(Color.Parse("#4FA8FF"));
    public static readonly IBrush GradeC = new SolidColorBrush(Color.Parse("#C77DFF"));
    public static readonly IBrush GradeD = new SolidColorBrush(Color.Parse("#FF6161"));
}
