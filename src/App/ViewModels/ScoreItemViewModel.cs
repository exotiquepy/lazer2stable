using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Exotique.Core;

namespace Exotique.App.ViewModels;

/// <summary>Une ligne de la liste des scores.</summary>
public sealed partial class ScoreItemViewModel : ObservableObject
{
    public ScoreItemViewModel(ScorePlanItem item, MapItemViewModel? map, MapItemViewModel? requiredMap)
    {
        Item = item;
        Map = map;
        RequiredMap = requiredMap;
        isSelected = item.Decision == ScoreDecision.Copy;

        var r = item.Replay;
        grade = r == null ? "" : ScoreMath.Grade(r);
        Accuracy = r == null ? 0 : ScoreMath.Accuracy(r);
        MaxCombo = r?.MaxCombo ?? 0;

        // Mods de lazer, sans « CL » (ajouté par lazer à tous les scores venus de stable).
        Mods = item.Score.Mods.Where(m => !m.Equals("CL", StringComparison.OrdinalIgnoreCase)).ToList();

        var set = item.Set;
        SearchText = string.Join('\n', new[]
        {
            set?.Title, set?.Artist, set?.TitleUnicode, set?.ArtistUnicode, item.Beatmap?.DifficultyName, item.Player, string.Join(" ", Mods),
        }.Where(s => !string.IsNullOrEmpty(s))).ToLowerInvariant();
    }

    public ScorePlanItem Item { get; }

    /// <summary>La map du score dans la liste des beatmaps (vignette, titre pendant la copie).</summary>
    public MapItemViewModel? Map { get; }

    /// <summary>Map à copier en même temps pour que ce score puisse aller dans stable.</summary>
    public MapItemViewModel? RequiredMap { get; }

    public double Accuracy { get; }
    public int MaxCombo { get; }
    public IReadOnlyList<string> Mods { get; }
    public bool HasMods => Mods.Count > 0;
    public string SearchText { get; }
    public DateTimeOffset Date => Item.Score.Date;
    public int RulesetId => Item.Score.RulesetId;
    public string MapSortKey => Item.Set?.Title ?? "";

    private readonly string grade;

    // ---------------------------------------------------------- catégorie

    /// <summary>Réglé par la liste : inclure les replays des autres joueurs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCopyCandidate), nameof(IsIgnored), nameof(ReasonText), nameof(NoteText), nameof(NoteBrush), nameof(HasNote))]
    private bool othersIncluded;

    public bool IsCopyCandidate => Item.Decision == ScoreDecision.Copy && (Item.IsMine || OthersIncluded);
    public bool IsPresent => Item.Decision == ScoreDecision.AlreadyPresent;
    public bool IsIgnored => Item.Decision == ScoreDecision.Ignored || (Item.Decision == ScoreDecision.Copy && !IsCopyCandidate);

    public string ReasonText => Item.Decision == ScoreDecision.Copy && !Item.IsMine
        ? Loc.F("ReasonScoreOther", Item.Player)
        : Item.Reason;

    // ----------------------------------------------------- map à copier aussi

    /// <summary>Réglé par la liste : la map nécessaire est cochée (et les beatmaps sont incluses).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(NoteText), nameof(NoteBrush), nameof(HasNote))]
    private bool mapComing;

    public bool NeedsMap => RequiredMap != null;

    /// <summary>Sera vraiment copié s'il est coché.</summary>
    public bool IsReady => !NeedsMap || MapComing;

    public string NoteText => IsPresent ? Item.Reason
        : IsCopyCandidate && NeedsMap ? Loc.T(MapComing ? "NeedsMapComing" : "NeedsMapMissing")
        : "";

    public bool HasNote => NoteText.Length > 0;

    public IBrush NoteBrush => IsPresent ? Palette.Stable : NeedsMap && !MapComing ? Palette.Warn : Palette.Muted;

    [ObservableProperty]
    private bool isSelected;

    // ---------------------------------------------------------------- textes

    public string Title
    {
        get
        {
            string title = Item.Set is { Title.Length: > 0 } s ? s.Title : Loc.T("NoTitle");
            return Item.Beatmap is { DifficultyName.Length: > 0 } b ? $"{title} [{b.DifficultyName}]" : title;
        }
    }

    public string SubLine
    {
        get
        {
            var parts = new List<string>();
            if (Item.Set is { Artist.Length: > 0 } s) parts.Add(s.Artist);
            parts.Add(Item.Player.Length > 0 ? Item.Player : "?");
            parts.Add(Date.ToLocalTime().ToString(Loc.T("ShortDate"), CultureInfo.InvariantCulture));
            return string.Join("  ·  ", parts);
        }
    }

    public string ScoreText => Fmt.N(Item.StableScore);

    public string AccComboText => Item.Replay == null ? "" : $"{Fmt.Percent(Accuracy)}  ·  {Fmt.N(MaxCombo)}x";

    /// <summary>Lettre du rang : SS, S, A… (l'argenté se voit à la couleur).</summary>
    public string GradeText => grade.TrimEnd('H');

    public IBrush GradeBrush => grade switch
    {
        "SSH" or "SH" => Palette.GradeSilver,
        "SS" or "S" => Palette.GradeGold,
        "A" => Palette.GradeA,
        "B" => Palette.GradeB,
        "C" => Palette.GradeC,
        _ => Palette.GradeD,
    };

    public string OriginText => Loc.T(Item.PlayedInLazer ? "PlayedInLazer" : "FromStable");

    internal void RefreshTexts()
    {
        foreach (string name in new[]
                 {
                     nameof(Title), nameof(SubLine), nameof(ScoreText), nameof(AccComboText), nameof(OriginText),
                     nameof(ReasonText), nameof(NoteText),
                 })
            OnPropertyChanged(name);
    }
}
