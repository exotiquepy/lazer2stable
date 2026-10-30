using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Exotique.Core;

namespace Exotique.App.ViewModels;

public enum Screen { Setup, Analyzing, Review, Exporting, Done }

public enum ListTab { ToCopy, Present, Ignored }

/// <summary>Ce qu'on regarde dans la liste : les beatmaps, les scores ou les skins.</summary>
public enum Section { Maps, Scores, Skins }

public sealed record SortOption(string Key, Func<IEnumerable<MapItemViewModel>, IEnumerable<MapItemViewModel>> Apply)
{
    public string Label => Loc.T(Key);

    public override string ToString() => Label;
}

public sealed record SkinSortOption(string Key, Func<IEnumerable<SkinItemViewModel>, IEnumerable<SkinItemViewModel>> Apply)
{
    public string Label => Loc.T(Key);

    public override string ToString() => Label;
}

public sealed record ScoreSortOption(string Key, Func<IEnumerable<ScoreItemViewModel>, IEnumerable<ScoreItemViewModel>> Apply)
{
    public string Label => Loc.T(Key);

    public override string ToString() => Label;
}

/// <summary>Ce que la fenêtre fournit au view-model (sélecteur de dossier).</summary>
public interface IShell
{
    Task<string?> PickFolderAsync(string title);
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly Settings settings = Settings.Load();
    private readonly DispatcherTimer osuWatch;
    private LazerLocation? lazer;
    private StableLocation? stable;
    private StableIndex? stableIndex;
    private Plan? plan;
    private ScorePlan? scorePlan;
    private List<LazerCollection> lazerCollections = [];
    private List<MapItemViewModel> allItems = [];
    private Dictionary<PlanItem, MapItemViewModel> byItem = [];
    private List<ScoreItemViewModel> allScores = [];
    private SkinPlan? skinPlan;
    private List<SkinItemViewModel> allSkins = [];
    private CancellationTokenSource? skinSearchDebounce;
    private CancellationTokenSource? exportCts;
    private CancellationTokenSource? searchDebounce;
    private CancellationTokenSource? scoreSearchDebounce;
    private List<OsuInstance> osuInstances = [];

    public IShell? Shell { get; set; }

    public MainViewModel()
    {
        if (Loc.Parse(settings.Language) is Lang saved)
            Loc.I.Lang = saved;
        Loc.I.LanguageChanged += RefreshTexts;

        exportAsOsz = settings.ExportAsOsz;
        exportCollections = settings.ExportCollections;
        includeMaps = settings.CopyBeatmaps;
        includeScores = settings.CopyScores;
        includeOthers = settings.ScoresIncludeOthers;
        includeSkins = settings.CopySkins;
        skinSortOptions = BuildSkinSortOptions();
        skinSort = skinSortOptions[0];
        lastCopy = CopyJournal.Load();
        sortOptions = BuildSortOptions();
        sort = sortOptions[0];
        scoreSortOptions = BuildScoreSortOptions();
        scoreSort = scoreSortOptions[0];

        // Ce constructeur démarre le minuteur aussitôt : on le met en pause jusqu'à StartAsync.
        osuWatch = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, (_, _) => CheckOsuRunning());
        osuWatch.Stop();
        RefreshLocations();
    }

    /// <summary>Premier lancement : détection des dossiers puis analyse si tout est trouvé.</summary>
    public async Task StartAsync()
    {
        CheckOsuRunning();
        osuWatch.Start();

        lazer = Paths.FindLazer(settings.LazerPath) ?? Paths.FindLazer();
        stable = Paths.FindStable(settings.StablePath) ?? Paths.FindStable();
        RefreshLocations();

        if (lazer != null && stable != null)
            await AnalyzeAsync();
        else
            CurrentScreen = Screen.Setup;
    }

    // ================================================================ langue

    public bool IsLangFr
    {
        get => Loc.I.Lang == Lang.Fr;
        set { if (value) SetLanguage(Lang.Fr); }
    }

    public bool IsLangEn
    {
        get => Loc.I.Lang == Lang.En;
        set { if (value) SetLanguage(Lang.En); }
    }

    public bool IsLangRu
    {
        get => Loc.I.Lang == Lang.Ru;
        set { if (value) SetLanguage(Lang.Ru); }
    }

    private void SetLanguage(Lang lang)
    {
        if (Loc.I.Lang == lang) return;
        settings.Language = Loc.Code(lang);
        settings.Save();
        Loc.I.Lang = lang; // déclenche RefreshTexts
    }

    /// <summary>Recalcule tous les textes construits en code après un changement de langue.</summary>
    private void RefreshTexts()
    {
        OnPropertyChanged(nameof(IsLangFr));
        OnPropertyChanged(nameof(IsLangEn));
        OnPropertyChanged(nameof(IsLangRu));

        RefreshLocations();
        LazerStep.RefreshTexts();
        SongsStep.RefreshTexts();
        CompareStep.RefreshTexts();
        if (CurrentScreen == Screen.Analyzing) SetAnalysisProgress(AnalysisProgress);
        else if (CurrentScreen == Screen.Review) CircleText = Fmt.N(CurrentSectionToCopy);

        RefreshCollectionsPreview();
        OsuRunningText = OsuProcesses.Describe(osuInstances);

        var current = Sort.Key;
        SortOptions = BuildSortOptions();
        Sort = SortOptions.First(o => o.Key == current);

        var currentScore = ScoreSort.Key;
        ScoreSortOptions = BuildScoreSortOptions();
        ScoreSort = ScoreSortOptions.First(o => o.Key == currentScore);

        foreach (var item in allItems) item.RefreshTexts();
        var currentSkin = SkinSort.Key;
        SkinSortOptions = BuildSkinSortOptions();
        SkinSort = SkinSortOptions.First(o => o.Key == currentSkin);

        foreach (var s in allScores) s.RefreshTexts();
        foreach (var k in allSkins) k.RefreshTexts();
        ApplyFilter();
        ApplyScoreFilter();
        ApplySkinFilter();
        RefreshScoreTexts();

        foreach (string name in new[]
                 {
                     nameof(Headline), nameof(SubHeadline), nameof(ToCopyCountText), nameof(PresentCountText), nameof(IgnoredCountText),
                     nameof(EmptyListText), nameof(VisibleCountText), nameof(ExportButtonText), nameof(SelectionText),
                     nameof(ScoreToCopyCountText), nameof(ScorePresentCountText), nameof(ScoreIgnoredCountText), nameof(ScoreEmptyText),
                     nameof(MapsSectionDetail), nameof(ScoresSectionDetail), nameof(SkinsSectionDetail),
                     nameof(SkinToCopyCountText), nameof(SkinPresentCountText), nameof(SkinIgnoredCountText), nameof(SkinEmptyText),
                     nameof(UndoLinkText),
                 })
            OnPropertyChanged(name);

        if (IsConfirmOpen) BuildConfirmTexts();
        if (CurrentScreen == Screen.Exporting) RefreshExportTexts();
        if (lastDone != null) BuildDoneTexts();
        if (IsUndoOpen) BuildUndoTexts();
    }

    // ================================================================ écrans

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetup), nameof(IsAnalyzing), nameof(IsReview), nameof(IsExporting), nameof(IsDone),
        nameof(ShowLocations), nameof(ShowCircle), nameof(CanChangeFolders), nameof(ShowOsuBanner))]
    private Screen currentScreen = Screen.Analyzing;

    public bool IsSetup => CurrentScreen == Screen.Setup;
    public bool IsAnalyzing => CurrentScreen == Screen.Analyzing;
    public bool IsReview => CurrentScreen == Screen.Review;
    public bool IsExporting => CurrentScreen == Screen.Exporting;
    public bool IsDone => CurrentScreen == Screen.Done;
    public bool ShowLocations => CurrentScreen is Screen.Review or Screen.Setup or Screen.Analyzing;
    public bool ShowCircle => CurrentScreen is Screen.Review or Screen.Exporting or Screen.Done;
    public bool CanChangeFolders => CurrentScreen is Screen.Review or Screen.Setup;

    // ============================================================== dossiers

    [ObservableProperty] private string lazerPathText = "";
    [ObservableProperty] private string stablePathText = "";
    [ObservableProperty] private bool lazerFound;
    [ObservableProperty] private bool stableFound;
    [ObservableProperty] private string? setupError;

    private void RefreshLocations()
    {
        LazerFound = lazer != null;
        StableFound = stable != null;
        LazerPathText = lazer?.StorageDir ?? Loc.T("LazerNotFound");
        StablePathText = stable?.SongsDir ?? Loc.T("StableNotFound");
    }

    [RelayCommand]
    private async Task ChangeLazerAsync()
    {
        if (Shell == null) return;
        string? picked = await Shell.PickFolderAsync(Loc.T("PickLazerTitle"));
        if (picked == null) return;

        var found = Paths.LazerAt(picked);
        if (found == null)
        {
            SetupError = Loc.F("ErrNoLazerHere", picked);
            CurrentScreen = Screen.Setup;
            return;
        }

        lazer = found;
        settings.LazerPath = found.StorageDir;
        settings.Save();
        await AfterFolderChangeAsync();
    }

    [RelayCommand]
    private async Task ChangeStableAsync()
    {
        if (Shell == null) return;
        string? picked = await Shell.PickFolderAsync(Loc.T("PickStableTitle"));
        if (picked == null) return;

        var found = Paths.StableAt(picked);
        if (found == null)
        {
            SetupError = Loc.F("ErrNoSongsHere", picked);
            CurrentScreen = Screen.Setup;
            return;
        }

        stable = found;
        settings.StablePath = found.InstallDir ?? found.SongsDir;
        settings.Save();
        await AfterFolderChangeAsync();
    }

    private async Task AfterFolderChangeAsync()
    {
        SetupError = null;
        RefreshLocations();
        if (lazer != null && stable != null)
            await AnalyzeAsync();
        else
            CurrentScreen = Screen.Setup;
    }

    // ============================================================== analyse

    /// <summary>Avancement global de l'analyse, de 0 à 1 (anneau autour du cercle).</summary>
    [ObservableProperty] private double analysisProgress;

    public AnalysisStep LazerStep { get; } = new("StepLazer");
    public AnalysisStep SongsStep { get; } = new("StepSongs");
    public AnalysisStep CompareStep { get; } = new("StepCompare");

    // Poids de chaque étape dans l'avancement global.
    private const double LazerWeight = 0.30, SongsWeight = 0.60, CompareWeight = 0.10;

    private void SetAnalysisProgress(double value)
    {
        AnalysisProgress = Math.Clamp(value, 0, 1);
        CircleText = $"{(int)Math.Floor(AnalysisProgress * 100)}%";
    }

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        if (lazer == null || stable == null)
        {
            CurrentScreen = Screen.Setup;
            return;
        }

        SetupError = null;
        lastDone = null;
        LazerStep.Reset();
        SongsStep.Reset();
        CompareStep.Reset();
        SetAnalysisProgress(0);
        CurrentScreen = Screen.Analyzing;
        CircleApproach = 0;

        try
        {
            var lz = lazer;
            var st = stable;

            // 1. Base lazer : copie temporaire puis lecture des sets et des scores.
            LazerStep.Start(() => Loc.T("StepCopyingDb"));
            var lazerProgress = new Progress<(double Fraction, int Sets, int Total)>(p =>
            {
                LazerStep.Fraction = p.Fraction;
                LazerStep.SetDetail(p.Total > 0 ? () => Loc.F("StepSetsProgress", Fmt.N(p.Sets), Fmt.N(p.Total)) : () => Loc.T("StepCopyingDb"));
                SetAnalysisProgress(LazerWeight * p.Fraction);
            });
            var data = await Task.Run(() => LazerLibrary.Read(lz, lazerProgress));
            int liveSets = data.Sets.Count(x => !x.DeletePending && !x.Protected);
            int scoreCount = data.Scores.Count, skinCount = data.Skins.Count;
            LazerStep.Finish(() => string.Join(", ", new[]
            {
                Loc.P("PMaps", liveSets),
                scoreCount > 0 ? Loc.P("PScores", scoreCount) : null,
                skinCount > 0 ? Loc.P("PSkins", skinCount) : null,
            }.Where(x => x != null)));
            SetAnalysisProgress(LazerWeight);

            // 2. Songs : inventaire des .osu, puis lecture de chacun.
            SongsStep.Start(() => Loc.T("StepInventory"));
            var scanProgress = new Progress<(int Done, int Total)>(p =>
            {
                if (p.Total < 0)
                {
                    SongsStep.SetDetail(() => Loc.F("StepInventoryCount", Fmt.N(p.Done)));
                    SongsStep.Fraction = 0;
                    return;
                }

                double f = p.Total == 0 ? 1 : (double)p.Done / p.Total;
                SongsStep.Fraction = f;
                SongsStep.SetDetail(() => Loc.F("StepDiffProgress", Fmt.N(p.Done), Fmt.N(p.Total)));
                SetAnalysisProgress(LazerWeight + SongsWeight * f);
            });
            var index = await Task.Run(() => StableIndex.Scan(st.SongsDir, scanProgress));
            SongsStep.Finish(() => Loc.P("PDifficulties", index.OsuFileCount));
            SetAnalysisProgress(LazerWeight + SongsWeight);

            // 3. Comparaison : les maps, puis les scores.
            CompareStep.Start(() => "");
            void Compare(double f)
            {
                CompareStep.Fraction = f;
                SetAnalysisProgress(LazerWeight + SongsWeight + CompareWeight * f);
            }

            var mapsProgress = new Progress<double>(f => Compare(f * 0.5));
            var scoresProgress = new Progress<double>(f => Compare(0.5 + f * 0.5));
            var built = await Task.Run(() => Planner.Build(data.Sets, index, lz, mapsProgress));
            var scores = await Task.Run(() => ScorePlanner.Build(data.Scores, data.Sets, lz, st, index, built, scoresProgress));
            var skins = await Task.Run(() => SkinPlanner.Build(data.Skins, lz, st));
            int missing = built.Count(Decision.Copy);
            int missingScores = scores.Items.Count(i => i.Decision == ScoreDecision.Copy && (i.IsMine || IncludeOthers));
            int missingSkins = skins.Items.Count(i => i.Decision == ScoreDecision.Copy);
            CompareStep.Finish(() => string.Join(", ", new[]
            {
                Loc.P("PMissingMaps", missing),
                missingScores > 0 ? Loc.P("PMissingScores", missingScores) : null,
                missingSkins > 0 ? Loc.P("PMissingSkins", missingSkins) : null,
            }.Where(x => x != null)));
            SetAnalysisProgress(1);

            stableIndex = index;
            plan = built;
            scorePlan = scores;
            lazerCollections = data.Collections;
            allItems = built.Items.Where(i => i.Decision != Decision.Skipped).Select(i => new MapItemViewModel(i, lz)).ToList();
            byItem = allItems.ToDictionary(v => v.Item);
            foreach (var item in allItems)
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MapItemViewModel.IsSelected)) RefreshSelection();
                };

            ToCopyCount = allItems.Count(i => i.IsCopyCandidate);
            PresentCount = allItems.Count(i => i.IsPresent);
            IgnoredCount = allItems.Count(i => i.IsIgnored);
            LazerSetCount = allItems.Count;
            ToCopyBytes = allItems.Where(i => i.IsCopyCandidate).Sum(i => i.Item.Bytes);

            BuildScoreItems(scores);
            BuildSkinItems(skins);
            LastCopy = CopyJournal.Load();

            RefreshCollectionsPreview();
            SelectedTab = ToCopyCount > 0 || PresentCount == 0 ? ListTab.ToCopy : ListTab.Present;
            SelectedScoreTab = ScoreToCopyCount > 0 || ScorePresentCount == 0 ? ListTab.ToCopy : ListTab.Present;
            SelectedSkinTab = SkinToCopyCount > 0 || SkinPresentCount == 0 ? ListTab.ToCopy : ListTab.Present;
            CurrentSection = ToCopyCount > 0 ? Section.Maps
                : ScoreToCopyCount > 0 ? Section.Scores
                : SkinToCopyCount > 0 ? Section.Skins
                : Section.Maps;
            ApplyFilter();
            ApplyScoreFilter();
            ApplySkinFilter();
            RefreshSelection();

            // Laisse l'anneau se remplir avant de passer à la liste.
            await Task.Delay(450);
            CircleText = Fmt.N(CurrentSectionToCopy);
            CurrentScreen = Screen.Review;
            // Le cercle d'approche se referme une fois sur le nombre d'éléments à copier.
            Dispatcher.UIThread.Post(() => CircleApproach = 1, DispatcherPriority.Background);
        }
        catch (UserFacingException e)
        {
            SetupError = e.Message;
            CurrentScreen = Screen.Setup;
        }
        catch (Exception e)
        {
            SetupError = Loc.F("ErrAnalysisFailed", e.Message);
            CurrentScreen = Screen.Setup;
        }
    }

    private void BuildScoreItems(ScorePlan scores)
    {
        var bySet = new Dictionary<LazerSet, MapItemViewModel>();
        foreach (var v in allItems) bySet.TryAdd(v.Item.Set, v);

        allScores = scores.Items.Select(i => new ScoreItemViewModel(i,
                i.Set != null ? bySet.GetValueOrDefault(i.Set) : null,
                i.NeedsMap != null ? byItem.GetValueOrDefault(i.NeedsMap) : null)
            {
                OthersIncluded = IncludeOthers,
            })
            .ToList();

        foreach (var s in allScores)
            s.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ScoreItemViewModel.IsSelected)) RefreshSelection();
            };

        ScoresAvailable = scores.Unavailable == null;
        ScoresUnavailableText = scores.Unavailable ?? "";
        OthersCount = allScores.Count(s => s.Item.Decision == ScoreDecision.Copy && !s.Item.IsMine);
        RecountScores();
        RefreshScoreTexts();
    }

    private void BuildSkinItems(SkinPlan skins)
    {
        skinPlan = skins;
        allSkins = skins.Items.Select(i => new SkinItemViewModel(i)).ToList();
        foreach (var k in allSkins)
            k.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SkinItemViewModel.IsSelected)) RefreshSelection();
            };

        SkinsAvailable = skins.Unavailable == null;
        SkinsUnavailableText = skins.Unavailable ?? "";
        SkinToCopyCount = allSkins.Count(k => k.IsCopyCandidate);
        SkinPresentCount = allSkins.Count(k => k.IsPresent);
        SkinIgnoredCount = allSkins.Count(k => k.IsIgnored);
    }

    private void RecountScores()
    {
        ScoreToCopyCount = allScores.Count(s => s.IsCopyCandidate);
        ScorePresentCount = allScores.Count(s => s.IsPresent);
        ScoreIgnoredCount = allScores.Count(s => s.IsIgnored);
    }

    private void RefreshScoreTexts()
    {
        IncludeOthersLabel = Loc.P("PIncludeOthers", OthersCount);
        var names = scorePlan?.PlayerNames ?? [];
        OthersDetail = Loc.F("OthersDetail", names.Count == 0 ? "—" : string.Join(", ", names.Select(Quote)));
        OnPropertyChanged(nameof(ScoresSectionDetail));
    }

    // ================================================================ résumé

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Headline), nameof(SubHeadline), nameof(NothingToCopy), nameof(ToCopyCountText),
        nameof(ExportButtonText), nameof(SelectionText), nameof(MapsSectionDetail))]
    private int toCopyCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubHeadline), nameof(PresentCountText))]
    private int presentCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubHeadline), nameof(IgnoredCountText))]
    private int ignoredCount;

    public string ToCopyCountText => Fmt.N(ToCopyCount);
    public string PresentCountText => Fmt.N(PresentCount);
    public string IgnoredCountText => Fmt.N(IgnoredCount);

    [ObservableProperty] private int lazerSetCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubHeadline))]
    private long toCopyBytes;

    public bool NothingToCopy => ToCopyCount == 0 && ScoreToCopyCount == 0 && SkinToCopyCount == 0;

    /// <summary>Les nombres non nuls, au pluriel demandé : « 12 maps », « 289 scores », « 3 skins ».</summary>
    private static List<string> Counted(string mapsKey, int maps, string scoresKey, int scores, string skinsKey, int skins)
    {
        var parts = new List<string>();
        if (maps > 0) parts.Add(Loc.P(mapsKey, maps));
        if (scores > 0) parts.Add(Loc.P(scoresKey, scores));
        if (skins > 0) parts.Add(Loc.P(skinsKey, skins));
        return parts;
    }

    public string Headline
    {
        get
        {
            int m = ToCopyCount, s = ScoreToCopyCount, k = SkinToCopyCount;
            var parts = Counted("PGenMaps", m, "PGenScores", s, "PGenSkins", k);
            if (parts.Count > 1) return Loc.F("HeadlineBoth", Loc.JoinAnd(parts));
            if (m > 0) return Loc.P("PHeadlineMissing", m);
            if (s > 0) return Loc.P("PHeadlineScores", s);
            if (k > 0) return Loc.P("PHeadlineSkins", k);
            return Loc.T(allScores.Count > 0 || allSkins.Count > 0 ? "HeadlineNoneAll" : "HeadlineNone");
        }
    }

    public string SubHeadline
    {
        get
        {
            if (IsSkinsSection)
            {
                if (!SkinsAvailable) return SkinsUnavailableText;
                string kp = Loc.P("PSubSkinsPresent", SkinPresentCount);
                string ki = SkinIgnoredCount == 0 ? "" : Loc.P("PSubSkinsIgnored", SkinIgnoredCount);
                return SkinToCopyCount == 0
                    ? $"{kp}{ki}. {Loc.T("SubNothing")}"
                    : $"{Loc.T("SubSkinsToCopy")} {kp}{ki}.";
            }

            if (IsScoresSection)
            {
                if (!ScoresAvailable) return ScoresUnavailableText;
                string sp = Loc.P("PSubScoresPresent", ScorePresentCount);
                string si = ScoreIgnoredCount == 0 ? "" : Loc.P("PSubScoresIgnored", ScoreIgnoredCount);
                return ScoreToCopyCount == 0
                    ? $"{sp}{si}. {Loc.T("SubNothing")}"
                    : $"{Loc.T("SubScoresToCopy")} {sp}{si}.";
            }

            string present = Loc.P("PSubPresent", PresentCount);
            string ignored = IgnoredCount == 0 ? "" : Loc.P("PSubIgnored", IgnoredCount);
            return ToCopyCount == 0
                ? $"{present}{ignored}. {Loc.T("SubNothing")}"
                : $"{Loc.F("SubToCopy", Fmt.Bytes(ToCopyBytes))} {present}{ignored}.";
        }
    }

    // ============================================================ approche

    /// <summary>0 : cercle d'approche loin ; 1 : refermé sur le cercle.</summary>
    [ObservableProperty] private double circleApproach;

    [ObservableProperty] private double circleBurst;

    [ObservableProperty] private string circleText = "";

    // ============================================================= sections

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMapsSection), nameof(IsScoresSection), nameof(IsSkinsSection), nameof(SubHeadline))]
    private Section currentSection;

    public bool IsMapsSection
    {
        get => CurrentSection == Section.Maps;
        set { if (value) CurrentSection = Section.Maps; }
    }

    public bool IsScoresSection
    {
        get => CurrentSection == Section.Scores;
        set { if (value) CurrentSection = Section.Scores; }
    }

    public bool IsSkinsSection
    {
        get => CurrentSection == Section.Skins;
        set { if (value) CurrentSection = Section.Skins; }
    }

    private int CurrentSectionToCopy => CurrentSection switch
    {
        Section.Scores => ScoreToCopyCount,
        Section.Skins => SkinToCopyCount,
        _ => ToCopyCount,
    };

    partial void OnCurrentSectionChanged(Section value)
    {
        if (CurrentScreen == Screen.Review) CircleText = Fmt.N(CurrentSectionToCopy);
    }

    /// <summary>Les beatmaps cochées sont copiées (décoché : seulement les scores, par exemple).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowMapsOff))]
    private bool includeMaps;

    partial void OnIncludeMapsChanged(bool value)
    {
        settings.CopyBeatmaps = value;
        settings.Save();
        RefreshSelection();
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowScoresOff))]
    private bool includeScores;

    public bool ShowMapsOff => !IncludeMaps;

    public bool ShowScoresOff => ScoresAvailable && !IncludeScores;

    partial void OnIncludeScoresChanged(bool value)
    {
        settings.CopyScores = value;
        settings.Save();
        RefreshSelection();
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowSkinsOff))]
    private bool includeSkins;

    partial void OnIncludeSkinsChanged(bool value)
    {
        settings.CopySkins = value;
        settings.Save();
        RefreshSelection();
    }

    public bool ShowSkinsOff => SkinsAvailable && !IncludeSkins;

    public string SkinsSectionDetail => !SkinsAvailable ? Loc.T("SectionUnavailable")
        : SkinToCopyCount > 0 ? Loc.P("PSectionToCopy", SkinToCopyCount)
        : Loc.T("SectionNone");

    public string MapsSectionDetail => ToCopyCount > 0 ? Loc.P("PSectionToCopy", ToCopyCount) : Loc.T("SectionNone");

    public string ScoresSectionDetail => !ScoresAvailable ? Loc.T("SectionUnavailable")
        : ScoreToCopyCount > 0 ? Loc.P("PSectionToCopy", ScoreToCopyCount)
        : Loc.T("SectionNone");

    // =================================================================== liste

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToCopyTab), nameof(IsPresentTab), nameof(IsIgnoredTab), nameof(EmptyListText))]
    private ListTab selectedTab;

    public bool IsToCopyTab
    {
        get => SelectedTab == ListTab.ToCopy;
        set { if (value) SelectedTab = ListTab.ToCopy; }
    }

    public bool IsPresentTab
    {
        get => SelectedTab == ListTab.Present;
        set { if (value) SelectedTab = ListTab.Present; }
    }

    public bool IsIgnoredTab
    {
        get => SelectedTab == ListTab.Ignored;
        set { if (value) SelectedTab = ListTab.Ignored; }
    }

    partial void OnSelectedTabChanged(ListTab value) => ApplyFilter();

    [ObservableProperty] private string searchText = "";

    partial void OnSearchTextChanged(string value)
    {
        searchDebounce?.Cancel();
        var cts = searchDebounce = new CancellationTokenSource();
        _ = Task.Delay(160, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) Dispatcher.UIThread.Post(ApplyFilter);
        }, TaskScheduler.Default);
    }

    [ObservableProperty] private bool showOsu = true;
    [ObservableProperty] private bool showTaiko = true;
    [ObservableProperty] private bool showCatch = true;
    [ObservableProperty] private bool showMania = true;

    partial void OnShowOsuChanged(bool value) => ApplyBothFilters();
    partial void OnShowTaikoChanged(bool value) => ApplyBothFilters();
    partial void OnShowCatchChanged(bool value) => ApplyBothFilters();
    partial void OnShowManiaChanged(bool value) => ApplyBothFilters();

    private void ApplyBothFilters()
    {
        ApplyFilter();
        ApplyScoreFilter();
    }

    private bool AllModes => ShowOsu && ShowTaiko && ShowCatch && ShowMania;

    private HashSet<int> ShownModes()
    {
        var modes = new HashSet<int>();
        if (ShowOsu) modes.Add(0);
        if (ShowTaiko) modes.Add(1);
        if (ShowCatch) modes.Add(2);
        if (ShowMania) modes.Add(3);
        return modes;
    }

    [ObservableProperty] private IReadOnlyList<SortOption> sortOptions;

    private static List<SortOption> BuildSortOptions() =>
    [
        new("SortRecent", s => s.OrderByDescending(i => i.DateAdded)),
        new("SortArtist", s => s.OrderBy(i => i.Artist, StringComparer.OrdinalIgnoreCase).ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)),
        new("SortTitle", s => s.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase)),
        new("SortMapper", s => s.OrderBy(i => i.Creator, StringComparer.OrdinalIgnoreCase)),
        new("SortStars", s => s.OrderByDescending(i => i.MaxStars)),
        new("SortSize", s => s.OrderByDescending(i => i.Item.Bytes)),
    ];

    [ObservableProperty] private SortOption sort;

    partial void OnSortChanged(SortOption value) => ApplyFilter();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListEmpty), nameof(VisibleCountText))]
    private IReadOnlyList<MapItemViewModel> visibleItems = [];

    public bool IsListEmpty => VisibleItems.Count == 0;

    public string VisibleCountText => VisibleItems.Count == CurrentTabTotal
        ? Loc.P("PMaps", VisibleItems.Count)
        : Loc.F("CountOf", Fmt.N(VisibleItems.Count), Fmt.N(CurrentTabTotal));

    private int CurrentTabTotal => SelectedTab switch
    {
        ListTab.ToCopy => ToCopyCount,
        ListTab.Present => PresentCount,
        _ => IgnoredCount,
    };

    public string EmptyListText => SearchText.Length > 0 || !AllModes
        ? Loc.T("EmptySearch")
        : SelectedTab switch
        {
            ListTab.ToCopy => Loc.T("EmptyToCopy"),
            ListTab.Present => Loc.T("EmptyPresent"),
            _ => Loc.T("EmptyIgnored"),
        };

    private void ApplyFilter()
    {
        if (Sort == null) return;

        IEnumerable<MapItemViewModel> q = SelectedTab switch
        {
            ListTab.ToCopy => allItems.Where(i => i.IsCopyCandidate),
            ListTab.Present => allItems.Where(i => i.IsPresent),
            _ => allItems.Where(i => i.IsIgnored),
        };

        if (!AllModes)
        {
            var modes = ShownModes();
            q = q.Where(i => i.Item.Set.Beatmaps.Any(b => modes.Contains(b.RulesetId)));
        }

        string search = SearchText.Trim().ToLowerInvariant();
        if (search.Length > 0)
        {
            string[] words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            q = q.Where(i => words.All(w => i.SearchText.Contains(w, StringComparison.Ordinal)));
        }

        VisibleItems = Sort.Apply(q).ToList();
        OnPropertyChanged(nameof(EmptyListText));
    }

    [RelayCommand]
    private void SelectAllVisible()
    {
        foreach (var i in VisibleItems.Where(i => i.IsCopyCandidate)) i.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoneVisible()
    {
        foreach (var i in VisibleItems.Where(i => i.IsCopyCandidate)) i.IsSelected = false;
    }

    // ============================================================ liste des scores

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ScoresSectionDetail), nameof(SubHeadline), nameof(ShowScoresOff))]
    private bool scoresAvailable = true;

    [ObservableProperty] private string scoresUnavailableText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Headline), nameof(SubHeadline), nameof(NothingToCopy), nameof(ScoreToCopyCountText),
        nameof(ExportButtonText), nameof(SelectionText), nameof(ScoresSectionDetail))]
    private int scoreToCopyCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubHeadline), nameof(ScorePresentCountText))]
    private int scorePresentCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubHeadline), nameof(ScoreIgnoredCountText))]
    private int scoreIgnoredCount;

    public string ScoreToCopyCountText => Fmt.N(ScoreToCopyCount);
    public string ScorePresentCountText => Fmt.N(ScorePresentCount);
    public string ScoreIgnoredCountText => Fmt.N(ScoreIgnoredCount);

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasOthers))]
    private int othersCount;

    public bool HasOthers => OthersCount > 0;

    [ObservableProperty] private string includeOthersLabel = "";
    [ObservableProperty] private string othersDetail = "";

    /// <summary>Copier aussi les replays d'autres joueurs (téléchargés ou regardés dans lazer).</summary>
    [ObservableProperty] private bool includeOthers;

    partial void OnIncludeOthersChanged(bool value)
    {
        settings.ScoresIncludeOthers = value;
        settings.Save();
        foreach (var s in allScores) s.OthersIncluded = value;
        RecountScores();
        if (CurrentScreen == Screen.Review && IsScoresSection) CircleText = Fmt.N(ScoreToCopyCount);
        ApplyScoreFilter();
        RefreshSelection();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsScoreToCopyTab), nameof(IsScorePresentTab), nameof(IsScoreIgnoredTab), nameof(ScoreEmptyText))]
    private ListTab selectedScoreTab;

    public bool IsScoreToCopyTab
    {
        get => SelectedScoreTab == ListTab.ToCopy;
        set { if (value) SelectedScoreTab = ListTab.ToCopy; }
    }

    public bool IsScorePresentTab
    {
        get => SelectedScoreTab == ListTab.Present;
        set { if (value) SelectedScoreTab = ListTab.Present; }
    }

    public bool IsScoreIgnoredTab
    {
        get => SelectedScoreTab == ListTab.Ignored;
        set { if (value) SelectedScoreTab = ListTab.Ignored; }
    }

    partial void OnSelectedScoreTabChanged(ListTab value) => ApplyScoreFilter();

    [ObservableProperty] private string scoreSearchText = "";

    partial void OnScoreSearchTextChanged(string value)
    {
        scoreSearchDebounce?.Cancel();
        var cts = scoreSearchDebounce = new CancellationTokenSource();
        _ = Task.Delay(160, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) Dispatcher.UIThread.Post(ApplyScoreFilter);
        }, TaskScheduler.Default);
    }

    [ObservableProperty] private IReadOnlyList<ScoreSortOption> scoreSortOptions;

    private static List<ScoreSortOption> BuildScoreSortOptions() =>
    [
        new("SortScoreRecent", s => s.OrderByDescending(i => i.Date)),
        new("SortScoreBest", s => s.OrderByDescending(i => i.Item.StableScore)),
        new("SortScoreAcc", s => s.OrderByDescending(i => i.Accuracy).ThenByDescending(i => i.Item.StableScore)),
        new("SortScoreMap", s => s.OrderBy(i => i.MapSortKey, StringComparer.OrdinalIgnoreCase).ThenByDescending(i => i.Item.StableScore)),
    ];

    [ObservableProperty] private ScoreSortOption scoreSort;

    partial void OnScoreSortChanged(ScoreSortOption value) => ApplyScoreFilter();

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsScoreListEmpty))]
    private IReadOnlyList<ScoreItemViewModel> visibleScores = [];

    public bool IsScoreListEmpty => VisibleScores.Count == 0;

    public string ScoreEmptyText => allScores.Count == 0 ? Loc.T("EmptyScoresNone")
        : ScoreSearchText.Length > 0 || !AllModes ? Loc.T("EmptyScoresSearch")
        : SelectedScoreTab switch
        {
            ListTab.ToCopy => Loc.T("EmptyScoresToCopy"),
            ListTab.Present => Loc.T("EmptyScoresPresent"),
            _ => Loc.T("EmptyScoresIgnored"),
        };

    private void ApplyScoreFilter()
    {
        if (ScoreSort == null) return;

        IEnumerable<ScoreItemViewModel> q = SelectedScoreTab switch
        {
            ListTab.ToCopy => allScores.Where(i => i.IsCopyCandidate),
            ListTab.Present => allScores.Where(i => i.IsPresent),
            _ => allScores.Where(i => i.IsIgnored),
        };

        if (!AllModes)
        {
            var modes = ShownModes();
            q = q.Where(i => modes.Contains(i.RulesetId));
        }

        string search = ScoreSearchText.Trim().ToLowerInvariant();
        if (search.Length > 0)
        {
            string[] words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            q = q.Where(i => words.All(w => i.SearchText.Contains(w, StringComparison.Ordinal)));
        }

        VisibleScores = ScoreSort.Apply(q).ToList();
        OnPropertyChanged(nameof(ScoreEmptyText));
    }

    [RelayCommand]
    private void SelectAllScores()
    {
        foreach (var i in VisibleScores.Where(i => i.IsCopyCandidate)) i.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoScores()
    {
        foreach (var i in VisibleScores.Where(i => i.IsCopyCandidate)) i.IsSelected = false;
    }

    // ============================================================ liste des skins

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SkinsSectionDetail), nameof(SubHeadline), nameof(ShowSkinsOff))]
    private bool skinsAvailable = true;

    [ObservableProperty] private string skinsUnavailableText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Headline), nameof(SubHeadline), nameof(NothingToCopy), nameof(SkinToCopyCountText),
        nameof(ExportButtonText), nameof(SelectionText), nameof(SkinsSectionDetail))]
    private int skinToCopyCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubHeadline), nameof(SkinPresentCountText))]
    private int skinPresentCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SubHeadline), nameof(SkinIgnoredCountText))]
    private int skinIgnoredCount;

    public string SkinToCopyCountText => Fmt.N(SkinToCopyCount);
    public string SkinPresentCountText => Fmt.N(SkinPresentCount);
    public string SkinIgnoredCountText => Fmt.N(SkinIgnoredCount);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkinToCopyTab), nameof(IsSkinPresentTab), nameof(IsSkinIgnoredTab), nameof(SkinEmptyText))]
    private ListTab selectedSkinTab;

    public bool IsSkinToCopyTab
    {
        get => SelectedSkinTab == ListTab.ToCopy;
        set { if (value) SelectedSkinTab = ListTab.ToCopy; }
    }

    public bool IsSkinPresentTab
    {
        get => SelectedSkinTab == ListTab.Present;
        set { if (value) SelectedSkinTab = ListTab.Present; }
    }

    public bool IsSkinIgnoredTab
    {
        get => SelectedSkinTab == ListTab.Ignored;
        set { if (value) SelectedSkinTab = ListTab.Ignored; }
    }

    partial void OnSelectedSkinTabChanged(ListTab value) => ApplySkinFilter();

    [ObservableProperty] private string skinSearchText = "";

    partial void OnSkinSearchTextChanged(string value)
    {
        skinSearchDebounce?.Cancel();
        var cts = skinSearchDebounce = new CancellationTokenSource();
        _ = Task.Delay(160, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) Dispatcher.UIThread.Post(ApplySkinFilter);
        }, TaskScheduler.Default);
    }

    [ObservableProperty] private IReadOnlyList<SkinSortOption> skinSortOptions;

    private static List<SkinSortOption> BuildSkinSortOptions() =>
    [
        new("SortSkinName", s => s.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase)),
        new("SortSkinSize", s => s.OrderByDescending(i => i.Item.Bytes)),
        new("SortSkinFiles", s => s.OrderByDescending(i => i.FileCount)),
    ];

    [ObservableProperty] private SkinSortOption skinSort;

    partial void OnSkinSortChanged(SkinSortOption value) => ApplySkinFilter();

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSkinListEmpty))]
    private IReadOnlyList<SkinItemViewModel> visibleSkins = [];

    public bool IsSkinListEmpty => VisibleSkins.Count == 0;

    public string SkinEmptyText => allSkins.Count == 0 ? Loc.T("EmptySkinsNone")
        : SkinSearchText.Length > 0 ? Loc.T("EmptySkinsSearch")
        : SelectedSkinTab switch
        {
            ListTab.ToCopy => Loc.T("EmptySkinsToCopy"),
            ListTab.Present => Loc.T("EmptySkinsPresent"),
            _ => Loc.T("EmptySkinsIgnored"),
        };

    private void ApplySkinFilter()
    {
        if (SkinSort == null) return;

        IEnumerable<SkinItemViewModel> q = SelectedSkinTab switch
        {
            ListTab.ToCopy => allSkins.Where(i => i.IsCopyCandidate),
            ListTab.Present => allSkins.Where(i => i.IsPresent),
            _ => allSkins.Where(i => i.IsIgnored),
        };

        string search = SkinSearchText.Trim().ToLowerInvariant();
        if (search.Length > 0)
        {
            string[] words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            q = q.Where(i => words.All(w => i.SearchText.Contains(w, StringComparison.Ordinal)));
        }

        VisibleSkins = SkinSort.Apply(q).ToList();
        OnPropertyChanged(nameof(SkinEmptyText));
    }

    [RelayCommand]
    private void SelectAllSkins()
    {
        foreach (var i in VisibleSkins.Where(i => i.IsCopyCandidate)) i.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNoSkins()
    {
        foreach (var i in VisibleSkins.Where(i => i.IsCopyCandidate)) i.IsSelected = false;
    }

    // ============================================================= sélection

    /// <summary>Maps cochées qui seront copiées (0 si les beatmaps sont décochées).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ExportButtonText), nameof(CanExport), nameof(SelectionText))]
    private int selectedCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SelectionText), nameof(NotEnoughSpace), nameof(CanExport))]
    private long selectedBytes;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SelectionText), nameof(NotEnoughSpace), nameof(CanExport))]
    private long? freeSpace;

    /// <summary>Scores cochés qui seront copiés (leur map est dans stable ou copiée en même temps).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ExportButtonText), nameof(CanExport), nameof(SelectionText))]
    private int selectedScoreCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ExportButtonText), nameof(CanExport), nameof(SelectionText))]
    private int selectedSkinCount;

    /// <summary>Scores cochés dont la map n'est ni dans stable ni cochée.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SelectionText))]
    private int waitingScoreCount;

    private bool refreshQueued;

    private void RefreshSelection()
    {
        // Regroupe les mises à jour quand « Tout cocher » touche des milliers de lignes.
        if (refreshQueued) return;
        refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            refreshQueued = false;
            var selected = IncludeMaps ? allItems.Where(i => i.IsCopyCandidate && i.IsSelected).ToList() : [];
            SelectedCount = selected.Count;
            SelectedBytes = selected.Sum(i => i.Item.Bytes);

            foreach (var s in allScores)
                if (s.NeedsMap)
                    s.MapComing = IncludeMaps && s.RequiredMap is { IsSelected: true, IsCopyCandidate: true };

            bool scoresOn = IncludeScores && ScoresAvailable;
            SelectedScoreCount = scoresOn ? allScores.Count(s => s.IsCopyCandidate && s.IsSelected && s.IsReady) : 0;
            WaitingScoreCount = scoresOn ? allScores.Count(s => s.IsCopyCandidate && s.IsSelected && !s.IsReady) : 0;
            SelectedSkinCount = IncludeSkins && SkinsAvailable ? allSkins.Count(k => k.IsCopyCandidate && k.IsSelected) : 0;

            if (stable != null) FreeSpace = Fmt.FreeSpace(stable.SongsDir);
            OnPropertyChanged(nameof(ExportButtonText));
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(SelectionText));
        }, DispatcherPriority.Background);
    }

    public bool NotEnoughSpace => SelectedCount > 0 && FreeSpace is long f && f < SelectedBytes + 200L * 1024 * 1024;

    /// <summary>Les collections seules (aucune map à copier) : possible tant que les beatmaps sont incluses.</summary>
    private bool CollectionsOnly => SelectedCount == 0 && SelectedScoreCount == 0 && SelectedSkinCount == 0 && IncludeMaps && WillExportCollections;

    public bool CanExport => (SelectedCount > 0 || SelectedScoreCount > 0 || SelectedSkinCount > 0 || CollectionsOnly) && !NotEnoughSpace;

    public string ExportButtonText
    {
        get
        {
            int m = SelectedCount, s = SelectedScoreCount, k = SelectedSkinCount;
            var parts = Counted("PAccMaps", m, "PAccScores", s, "PAccSkins", k);
            if (parts.Count > 1) return Loc.F("ExportBothButton", Loc.JoinAnd(parts));
            if (m > 0) return Loc.P("PExportButton", m);
            if (s > 0) return Loc.P("PExportScoresButton", s);
            if (k > 0) return Loc.P("PExportSkinsButton", k);
            if (CollectionsOnly) return Loc.T("AddCollectionsButton");
            return NothingToCopy ? Loc.T("NothingToCopy") : Loc.T("NoneChecked");
        }
    }

    public string SelectionText
    {
        get
        {
            if (NothingToCopy && !CollectionsOnly) return Loc.T("SelAllPresent");

            var parts = new List<string>();
            if (SelectedCount > 0) parts.Add(Loc.F("SelMapsSeg", Loc.P("PSelChecked", SelectedCount), Fmt.Bytes(SelectedBytes)));
            if (SelectedScoreCount > 0) parts.Add(Loc.P("PSelScoresChecked", SelectedScoreCount));
            if (SelectedSkinCount > 0) parts.Add(Loc.P("PSelSkinsChecked", SelectedSkinCount));
            if (WaitingScoreCount > 0) parts.Add(Loc.P("PSelScoresWaiting", WaitingScoreCount));
            if (parts.Count == 0) return Loc.T(CollectionsOnly ? "SelCollectionsOnly" : "SelCheckAnything");

            string sel = Capitalize(string.Join(", ", parts)) + ".";
            if (SelectedCount == 0 || FreeSpace is not long f) return sel;
            return NotEnoughSpace ? Loc.F("SelNoSpace", sel, Fmt.Bytes(f)) : Loc.F("SelFree", sel, Fmt.Bytes(f));
        }
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Guillemets de la langue : « nom » (fr), «nom» (ru), "name" (en).</summary>
    private static string Quote(string s) => Loc.I.Lang switch
    {
        Lang.En => $"\"{s}\"",
        Lang.Ru => $"«{s}»",
        _ => $"« {s} »",
    };

    // =============================================================== options

    [ObservableProperty] private bool exportAsOsz;

    partial void OnExportAsOszChanged(bool value)
    {
        settings.ExportAsOsz = value;
        settings.Save();
        OnPropertyChanged(nameof(FolderFormatChosen));
        OnPropertyChanged(nameof(OszFormatChosen));
    }

    // Boutons radio du format : on n'écoute que les « coché », sinon les deux boutons se renvoient la valeur sans fin.
    public bool FolderFormatChosen
    {
        get => !ExportAsOsz;
        set { if (value) ExportAsOsz = false; }
    }

    public bool OszFormatChosen
    {
        get => ExportAsOsz;
        set { if (value) ExportAsOsz = true; }
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(WillExportCollections))]
    private bool exportCollections;

    partial void OnExportCollectionsChanged(bool value)
    {
        settings.ExportCollections = value;
        settings.Save();
        RefreshSelection();
    }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(WillExportCollections))]
    private bool collectionsAvailable;

    [ObservableProperty] private string collectionsLabel = "";
    [ObservableProperty] private string collectionsDetail = "";

    public bool WillExportCollections => ExportCollections && CollectionsAvailable;

    private void RefreshCollectionsPreview()
    {
        int n = lazerCollections.Count;
        CollectionsLabel = n == 0 ? Loc.T("GroupCollections") : Loc.P("PCollectionsLabel", n);

        if (n == 0)
        {
            CollectionsAvailable = false;
            CollectionsDetail = Loc.T("CollNone");
            return;
        }

        if (stable?.CollectionDbPath == null)
        {
            CollectionsAvailable = false;
            CollectionsDetail = Loc.T("CollNoInstall");
            return;
        }

        try
        {
            var (added, updated, _) = StableCollections.Preview(stable.CollectionDbPath, lazerCollections);
            CollectionsAvailable = added + updated > 0;
            CollectionsDetail = added + updated == 0
                ? Loc.T("CollAllPresent")
                : Loc.F("CollPreview", Loc.P("PCollNew", added), Loc.P("PCollUpdated", updated));
        }
        catch (Exception e)
        {
            CollectionsAvailable = false;
            CollectionsDetail = Loc.F("CollUnreadable", e.Message);
        }
    }

    // ============================================================ osu! ouvert

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowOsuBanner))]
    private bool osuRunning;

    [ObservableProperty] private string osuRunningText = "";
    [ObservableProperty] private bool stableRunning;
    [ObservableProperty] private bool isClosingOsu;
    [ObservableProperty] private bool osuCloseFailed;

    public bool ShowOsuBanner => OsuRunning && CanChangeFolders;

    private void CheckOsuRunning()
    {
        osuInstances = OsuProcesses.Running();
        OsuRunning = osuInstances.Count > 0;
        StableRunning = osuInstances.Any(r => r.Kind == OsuKind.Stable);
        OsuRunningText = OsuProcesses.Describe(osuInstances);
        if (!OsuRunning) OsuCloseFailed = false;
    }

    /// <summary>Ferme osu! comme un clic sur la croix : osu! enregistre tout avant de quitter.</summary>
    private async Task<bool> CloseOsuCoreAsync()
    {
        IsClosingOsu = true;
        OsuCloseFailed = false;
        try
        {
            var left = await OsuProcesses.CloseGracefullyAsync(TimeSpan.FromSeconds(15));
            CheckOsuRunning();
            OsuCloseFailed = left.Count > 0;
            return left.Count == 0;
        }
        finally
        {
            IsClosingOsu = false;
        }
    }

    [RelayCommand]
    private async Task CloseOsuAsync() => await CloseOsuCoreAsync();

    [RelayCommand]
    private async Task ForceCloseOsuAsync()
    {
        IsClosingOsu = true;
        try
        {
            await Task.Run(OsuProcesses.Kill);
            CheckOsuRunning();
            OsuCloseFailed = OsuRunning;
        }
        finally
        {
            IsClosingOsu = false;
        }
    }

    [RelayCommand]
    private async Task CloseOsuThenExportAsync()
    {
        if (await CloseOsuCoreAsync())
            await ConfirmExportAsync();
    }

    // ========================================================== confirmation

    [ObservableProperty] private bool isConfirmOpen;
    [ObservableProperty] private string confirmTitle = "";
    [ObservableProperty] private string confirmBody = "";
    [ObservableProperty] private string confirmDestination = "";
    [ObservableProperty] private string confirmCollections = "";

    [RelayCommand]
    private void AskExport()
    {
        if (!CanExport || stable == null) return;
        CheckOsuRunning();
        BuildConfirmTexts();
        IsConfirmOpen = true;
    }

    private bool WillCopyCollections => IncludeMaps && WillExportCollections;

    private void BuildConfirmTexts()
    {
        if (stable == null) return;

        int m = SelectedCount, s = SelectedScoreCount, k = SelectedSkinCount;
        var titleParts = Counted("PAccMaps", m, "PAccScores", s, "PAccSkins", k);
        ConfirmTitle = titleParts.Count > 1 ? Loc.F("ConfirmBothTitle", Loc.JoinAnd(titleParts))
            : m > 0 ? Loc.P("PConfirmTitle", m)
            : s > 0 ? Loc.P("PConfirmScoresTitle", s)
            : k > 0 ? Loc.P("PConfirmSkinsTitle", k)
            : Loc.T("ConfirmCollectionsTitle");

        var body = new List<string>();
        var destinations = new List<string>();
        if (m > 0)
        {
            string after = FreeSpace is long f ? Loc.F("ConfirmAfter", Fmt.Bytes(Math.Max(0, f - SelectedBytes))) : "";
            body.Add(Loc.F(ExportAsOsz ? "ConfirmBodyOsz" : "ConfirmBodyFolders", Fmt.Bytes(SelectedBytes), after));
            destinations.Add(stable.SongsDir);
        }

        if (s > 0)
        {
            body.Add(Loc.T("ConfirmScores"));
            if (stable.InstallDir != null) destinations.Add(stable.InstallDir);
        }

        if (k > 0)
        {
            body.Add(Loc.T("ConfirmSkins"));
            if (skinPlan?.SkinsDir != null) destinations.Add(skinPlan.SkinsDir);
        }

        if (m == 0 && s == 0 && k == 0 && stable.InstallDir != null) destinations.Add(stable.InstallDir);

        ConfirmBody = string.Join("\n", body);
        ConfirmDestination = string.Join("\n", destinations.Distinct());
        ConfirmCollections = WillCopyCollections ? Loc.F("ConfirmCollections", CollectionsDetail) : "";
    }

    [RelayCommand]
    private void CancelExport()
    {
        if (IsErrorOpen)
        {
            IsErrorOpen = false;
            return;
        }

        if (IsInfoOpen)
        {
            IsInfoOpen = false;
            return;
        }

        if (IsUndoOpen)
        {
            if (!IsUndoing) IsUndoOpen = false;
            return;
        }

        IsConfirmOpen = false;
    }

    // ============================================================ erreurs

    [ObservableProperty] private bool isErrorOpen;
    [ObservableProperty] private string errorText = "";
    [ObservableProperty] private string? errorLogPath;

    public void ShowError(string message, string? logPath)
    {
        ErrorText = message;
        ErrorLogPath = logPath;
        IsErrorOpen = true;
    }

    [RelayCommand]
    private void OpenErrorLog()
    {
        if (ErrorLogPath != null) OpenPath(ErrorLogPath);
    }

    [RelayCommand]
    private void DismissError() => IsErrorOpen = false;

    // ================================================================ export

    private enum ExportPhase { Maps, Collections, Skins, Scores }

    [ObservableProperty] private string exportHeadline = "";
    [ObservableProperty] private string exportDetail = "";
    [ObservableProperty] private MapItemViewModel? currentExport;
    [ObservableProperty] private bool isStopping;
    [ObservableProperty] private bool canStop = true;

    public ObservableCollection<MapItemViewModel> RecentlyExported { get; } = [];

    private ExportPhase exportPhase = ExportPhase.Maps;
    private ExportProgress? lastProgress;
    private ScoreExportProgress? lastScoreProgress;
    private (int Done, int Total)? lastSkinProgress;
    private TimeSpan? lastEta;

    private void RefreshExportTexts()
    {
        switch (exportPhase)
        {
            case ExportPhase.Collections:
                ExportHeadline = Loc.T("ExportCollectionsHeadline");
                ExportDetail = "";
                return;

            case ExportPhase.Skins:
                ExportHeadline = Loc.T("ExportSkinsHeadline");
                ExportDetail = lastSkinProgress is { } kp ? Loc.F("ExportScoresDetail", Fmt.N(kp.Done), Fmt.N(kp.Total)) : "";
                return;

            case ExportPhase.Scores:
                ExportHeadline = Loc.T("ExportScoresHeadline");
                ExportDetail = lastScoreProgress is { } sp ? Loc.F("ExportScoresDetail", Fmt.N(sp.Done), Fmt.N(sp.Total)) : "";
                return;
        }

        ExportHeadline = Loc.T("ExportHeadline");
        if (lastProgress is not { } p) return;

        string eta = lastEta is { } t ? Loc.F("ExportEta", Fmt.Duration(t)) : "";
        ExportDetail = Loc.F("ExportDetail", Fmt.N(p.Done), Fmt.N(p.Total), Fmt.Bytes(p.BytesDone), Fmt.Bytes(p.BytesTotal), eta);
    }

    [RelayCommand]
    private async Task ConfirmExportAsync()
    {
        IsConfirmOpen = false;
        if (stable == null || lazer == null || stableIndex == null || plan == null) return;
        CheckOsuRunning();
        bool stableWasRunning = StableRunning;

        var selectedVms = IncludeMaps
            ? allItems.Where(i => i.IsCopyCandidate && i.IsSelected).OrderBy(i => i.Artist, StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        var selected = selectedVms.Select(i => i.Item).ToList();
        int total = selected.Count;
        long totalBytes = selected.Sum(i => i.Bytes);

        var selectedScoreVms = IncludeScores && ScoresAvailable
            ? allScores.Where(s => s.IsCopyCandidate && s.IsSelected && s.IsReady).ToList()
            : [];
        var selectedScores = selectedScoreVms.Select(s => s.Item).ToList();
        var selectedSkins = IncludeSkins && SkinsAvailable
            ? allSkins.Where(k => k.IsCopyCandidate && k.IsSelected).Select(k => k.Item).ToList()
            : [];
        bool doCollections = WillCopyCollections;

        RecentlyExported.Clear();
        IsStopping = false;
        CanStop = total > 0;
        CircleApproach = 0;
        CircleBurst = 0;
        CircleText = "0";
        exportPhase = total > 0 ? ExportPhase.Maps
            : selectedSkins.Count > 0 ? ExportPhase.Skins
            : selectedScores.Count > 0 ? ExportPhase.Scores
            : ExportPhase.Collections;
        lastSkinProgress = (0, selectedSkins.Count);
        lastProgress = new ExportProgress(0, total, null, 0, totalBytes);
        lastScoreProgress = new ScoreExportProgress(0, selectedScores.Count, null);
        lastEta = null;
        RefreshExportTexts();
        CurrentScreen = Screen.Exporting;
        osuWatch.Stop();

        exportCts = new CancellationTokenSource();
        var sw = Stopwatch.StartNew();
        int lastDoneCount = 0;

        var progress = new Progress<ExportProgress>(p =>
        {
            // Les maps terminées depuis la dernière mise à jour rejoignent le journal.
            for (int k = lastDoneCount; k < p.Done && k < selectedVms.Count; k++)
            {
                RecentlyExported.Insert(0, selectedVms[k]);
                if (RecentlyExported.Count > 120) RecentlyExported.RemoveAt(RecentlyExported.Count - 1);
            }

            lastDoneCount = p.Done;
            CurrentExport = p.Current == null ? null : byItem.GetValueOrDefault(p.Current);
            CircleText = Fmt.N(p.Done);
            CircleApproach = total == 0 ? 1 : (double)p.Done / total;

            lastEta = null;
            if (p.BytesDone > 0 && sw.Elapsed.TotalSeconds > 3 && p.Done < total)
            {
                double rate = p.BytesDone / sw.Elapsed.TotalSeconds;
                lastEta = TimeSpan.FromSeconds((totalBytes - p.BytesDone) / Math.Max(1, rate));
            }

            lastProgress = p with { BytesTotal = totalBytes };
            RefreshExportTexts();
        });

        var lz = lazer;
        var st = stable;
        var idx = stableIndex;
        string? oszDir = ExportAsOsz ? st.SongsDir : null;
        var token = exportCts.Token;

        // 1. Les maps.
        var result = new ExportResult();
        if (total > 0)
        {
            try
            {
                result = await Task.Run(() => Exporter.Run(selected, lz, st.SongsDir, oszDir, idx, progress, token));
            }
            catch (Exception e)
            {
                result = new ExportResult();
                result.Failed.Add((selected[0], e.Message));
            }
        }

        // 2. Les collections.
        CollectionsExportResult? coll = null;
        string? collError = null;
        if (doCollections && stableWasRunning)
        {
            // stable réécrit collection.db en quittant : écrire maintenant serait perdu.
            collError = Loc.T("CollStableWasOpen");
        }
        else if (doCollections && st.CollectionDbPath != null && !result.DiskFull)
        {
            exportPhase = ExportPhase.Collections;
            RefreshExportTexts();
            try
            {
                var lc = lazerCollections;
                coll = await Task.Run(() => StableCollections.Export(st.CollectionDbPath, lc));
            }
            catch (Exception e)
            {
                collError = e.Message;
            }
        }

        // 3. Les skins.
        SkinExportResult? skinResult = null;
        if (selectedSkins.Count > 0 && skinPlan?.SkinsDir is string skinsDir && !result.Cancelled && !result.DiskFull)
        {
            exportPhase = ExportPhase.Skins;
            CanStop = false;
            CircleApproach = 0;
            CircleText = "0";
            RefreshExportTexts();
            var skinProgress = new Progress<(int Done, int Total, SkinPlanItem? Current)>(p =>
            {
                lastSkinProgress = (p.Done, p.Total);
                CircleText = Fmt.N(p.Done);
                CircleApproach = p.Total == 0 ? 1 : (double)p.Done / p.Total;
                RefreshExportTexts();
            });
            try
            {
                skinResult = await Task.Run(() => SkinExporter.Run(selectedSkins, lz, skinsDir, skinProgress));
            }
            catch (Exception e)
            {
                skinResult = new SkinExportResult();
                skinResult.Failed.Add((selectedSkins[0], e.Message));
            }
        }

        // 4. Les scores : après les maps, pour que ceux des maps tout juste copiées puissent suivre.
        ScoreExportResult? scoreResult = null;
        string? scoreError = null;
        if (selectedScores.Count > 0)
        {
            if (stableWasRunning)
                scoreError = Loc.T("ScoresStableWasOpen");
            else if (st.InstallDir == null)
                scoreError = Loc.T("ScoresNoInstall");
            else if (result.Cancelled || result.DiskFull)
                scoreError = null; // copie interrompue : on n'ajoute pas les scores, l'utilisateur relancera
            else
            {
                exportPhase = ExportPhase.Scores;
                CanStop = false;
                CircleApproach = 0;
                CircleText = "0";
                RefreshExportTexts();

                var available = new HashSet<string>(idx.Md5s, StringComparer.OrdinalIgnoreCase);
                foreach (var (item, _) in result.Exported)
                    foreach (var b in item.Set.Beatmaps)
                        available.Add(b.Md5);

                var scoreVmByItem = selectedScoreVms.ToDictionary(v => v.Item);
                var scoreProgress = new Progress<ScoreExportProgress>(p =>
                {
                    lastScoreProgress = p;
                    if (p.Current != null && scoreVmByItem.TryGetValue(p.Current, out var vm)) CurrentExport = vm.Map;
                    CircleText = Fmt.N(p.Done);
                    CircleApproach = p.Total == 0 ? 1 : (double)p.Done / p.Total;
                    RefreshExportTexts();
                });

                int version = scorePlan?.StableVersion ?? StableScoresDb.FallbackVersion;
                string installDir = st.InstallDir;
                try
                {
                    scoreResult = await Task.Run(() => ScoreExporter.Run(selectedScores, lz, installDir, available, version, scoreProgress));
                }
                catch (Exception e)
                {
                    scoreError = e.Message;
                }
            }
        }

        try
        {
            ReportPath = Report.Write(lz, st, plan, selected, result, coll, collError, scorePlan, scoreResult, scoreError, skinPlan, skinResult);
        }
        catch
        {
            ReportPath = null;
        }

        // Ce qui a été ajouté, pour pouvoir l'annuler.
        var journal = new CopyJournal
        {
            SongsDir = st.SongsDir,
            InstallDir = st.InstallDir,
            Maps = result.Exported.Select(e => e.Destination).ToList(),
            Skins = skinResult?.Exported.Select(e => e.Destination).ToList() ?? [],
            Scores = scoreResult?.Journal.ToList() ?? [],
            AddedCollections = coll?.AddedNames ?? [],
            AddedToCollections = coll?.AddedHashes ?? [],
        };
        if (!journal.IsEmpty)
        {
            journal.Save();
            LastCopy = journal;
        }

        ShowDone(new DoneState(result, coll, collError, total, scoreResult, scoreError, selectedScores.Count, skinResult, selectedSkins.Count, sw.Elapsed));
    }

    [RelayCommand]
    private void StopExport()
    {
        IsStopping = true;
        exportCts?.Cancel();
    }

    // ================================================================= bilan

    [ObservableProperty] private string doneHeadline = "";
    [ObservableProperty] private string doneDetail = "";
    [ObservableProperty] private string doneCollections = "";
    [ObservableProperty] private string doneScores = "";
    [ObservableProperty] private string doneSkins = "";
    [ObservableProperty] private string? reportPath;
    [ObservableProperty] private bool hasFailures;
    [ObservableProperty] private string nextStepText = "";

    public ObservableCollection<string> DoneProblems { get; } = [];

    private sealed record DoneState(
        ExportResult Result,
        CollectionsExportResult? Collections,
        string? CollectionsError,
        int Total,
        ScoreExportResult? Scores,
        string? ScoresError,
        int TotalScores,
        SkinExportResult? Skins,
        int TotalSkins,
        TimeSpan Elapsed);

    private DoneState? lastDone;

    private void ShowDone(DoneState done)
    {
        lastDone = done;
        BuildDoneTexts();

        CircleText = Fmt.N(done.Result.Exported.Count + (done.Scores?.Added.Count ?? 0) + (done.Skins?.Exported.Count ?? 0));
        CurrentExport = null;
        OnPropertyChanged(nameof(CanLaunchStable));
        CurrentScreen = Screen.Done;
        CircleApproach = 1;
        Dispatcher.UIThread.Post(() => CircleBurst = 1, DispatcherPriority.Background);
    }

    private void BuildDoneTexts()
    {
        if (lastDone is not { } d) return;
        var result = d.Result;
        int n = result.Exported.Count;
        int s = d.Scores?.Added.Count ?? 0;
        int k = d.Skins?.Exported.Count ?? 0;

        var parts0 = Counted("PNomMaps", n, "PNomScores", s, "PNomSkins", k);
        DoneHeadline = parts0.Count > 1 ? Loc.F("DoneBoth", Loc.JoinAnd(parts0))
            : n > 0 ? Loc.P("PDoneHeadline", n)
            : s > 0 ? Loc.P("PDoneScoresHeadline", s)
            : k > 0 ? Loc.P("PDoneSkinsHeadline", k)
            : Loc.T(d.Total > 0 && d.TotalScores == 0 && d.TotalSkins == 0 ? "DoneNone" : "DoneNothing");

        var parts = new List<string>();
        if (d.Total > 0)
        {
            parts.Add(Loc.F("DoneBytes", Fmt.Bytes(result.BytesCopied), Fmt.Duration(d.Elapsed)));
            if (result.Cancelled) parts.Add(Loc.F("DoneCancelled", Loc.P("PMaps", d.Total - n - result.Failed.Count)));
            if (result.DiskFull) parts.Add(Loc.T("DoneDiskFull"));
            if (result.Failed.Count > 0) parts.Add(Loc.P("PDoneFailed", result.Failed.Count));
            if (result.Warnings.Count > 0) parts.Add(Loc.P("PDoneWarnings", result.Warnings.Count));
        }

        DoneDetail = string.Join(" ", parts);

        DoneCollections = d.Collections is { } c
            ? Loc.F("DoneCollections", Loc.P("PCollAdded", c.Added), Loc.P("PCollUpdated", c.Updated))
              + (c.BackupPath != null ? Loc.F("DoneBackup", Path.GetFileName(c.BackupPath)) : "")
            : d.CollectionsError != null ? Loc.F("DoneCollFailed", d.CollectionsError) : "";

        if (d.Scores is { Error: null } sr)
        {
            DoneScores = Loc.P("PDoneScores", sr.Added.Count)
                         + (sr.BackupPath != null ? Loc.F("DoneScoresBackup", Path.GetFileName(sr.BackupPath)) : "")
                         + (sr.MissingMap.Count > 0 ? Loc.P("PDoneScoresMissingMap", sr.MissingMap.Count) : "")
                         + (sr.Failed.Count > 0 ? Loc.P("PDoneScoresFailedCount", sr.Failed.Count) : "");
        }
        else
        {
            string? error = d.Scores?.Error ?? d.ScoresError;
            DoneScores = error != null ? Loc.F("DoneScoresFailed", error) : "";
        }

        DoneSkins = d.Skins == null ? ""
            : Loc.P("PDoneSkins", d.Skins.Exported.Count) + (d.Skins.Failed.Count > 0 ? Loc.P("PDoneSkinsFailed", d.Skins.Failed.Count) : "");

        DoneProblems.Clear();
        foreach (var (item, error) in result.Failed) DoneProblems.Add(Loc.F("Line", item.Set.DisplayName, error));
        foreach (var w in result.Warnings) DoneProblems.Add(w.ToString());
        if (d.Scores is { Error: null })
            foreach (var (item, error) in d.Scores.Failed)
                DoneProblems.Add(Loc.F("Line", item.DisplayName, error));
        if (d.Skins != null)
            foreach (var (item, error) in d.Skins.Failed)
                DoneProblems.Add(Loc.F("Line", item.Skin.DisplayName, error));
        HasFailures = DoneProblems.Count > 0;

        var next = new List<string>();
        if (n > 0 || (s == 0 && k == 0)) next.Add(Loc.T(ExportAsOsz ? "NextOsz" : "NextFolders"));
        else next.Add(Loc.T("NextLaunch"));
        if (s > 0) next.Add(Loc.T("NextScores"));
        if (k > 0) next.Add(Loc.T("NextSkins"));
        NextStepText = string.Join(" ", next);
    }

    [RelayCommand]
    private void OpenSongs()
    {
        if (stable != null) OpenPath(stable.SongsDir);
    }

    [RelayCommand]
    private void OpenReport()
    {
        if (ReportPath != null) OpenPath(ReportPath);
    }

    public bool CanLaunchStable => stable?.ExePath != null && File.Exists(stable.ExePath);

    [RelayCommand]
    private void LaunchStable()
    {
        if (stable?.ExePath is string exe && File.Exists(exe))
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! })?.Dispose();
            }
            catch
            {
                // rien de grave : l'utilisateur peut lancer stable lui-même
            }
        }
    }

    [RelayCommand]
    private async Task BackToListAsync()
    {
        osuWatch.Start();
        await AnalyzeAsync();
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch
        {
            // pas d'explorateur disponible
        }
    }

    // ============================================================ annulation

    /// <summary>La dernière copie, qu'on peut annuler (null s'il n'y en a pas).</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasLastCopy), nameof(UndoLinkText))]
    private CopyJournal? lastCopy;

    public bool HasLastCopy => LastCopy != null;

    public string UndoLinkText => LastCopy == null ? ""
        : Loc.F("UndoLink", LastCopy.Date.ToString(Loc.T("UndoDate"), System.Globalization.CultureInfo.InvariantCulture));

    [ObservableProperty] private bool isUndoOpen;
    [ObservableProperty] private string undoBody = "";
    [ObservableProperty] private bool isUndoing;

    [ObservableProperty] private bool isInfoOpen;
    [ObservableProperty] private string infoTitle = "";
    [ObservableProperty] private string infoText = "";

    [RelayCommand]
    private void AskUndo()
    {
        if (LastCopy == null) return;
        CheckOsuRunning();
        BuildUndoTexts();
        IsUndoOpen = true;
    }

    private void BuildUndoTexts()
    {
        if (LastCopy is not { } j) return;
        var parts = Counted("PNomMaps", j.Maps.Count, "PNomScores", j.Scores.Count, "PNomSkins", j.Skins.Count);
        if (j.HasCollections) parts.Add(Loc.T("UndoCollections"));
        UndoBody = Loc.F("UndoBody", Loc.JoinAnd(parts));
    }

    [RelayCommand]
    private void CancelUndo() => IsUndoOpen = false;

    [RelayCommand]
    private async Task CloseOsuThenUndoAsync()
    {
        if (await CloseOsuCoreAsync())
            await ConfirmUndoAsync();
    }

    [RelayCommand]
    private async Task ConfirmUndoAsync()
    {
        if (LastCopy is not { } journal) return;
        CheckOsuRunning();
        if (StableRunning) return; // le dialogue propose de fermer stable d'abord

        IsUndoing = true;
        UndoResult result;
        try
        {
            result = await Task.Run(() => CopyUndo.Run(journal));
        }
        finally
        {
            IsUndoing = false;
            IsUndoOpen = false;
        }

        var parts = Counted("PNomMaps", result.Maps, "PNomScores", result.Scores, "PNomSkins", result.Skins);
        if (result.Collections > 0) parts.Add(Loc.P("PCollections", result.Collections));
        if (result.CollectionMaps > 0) parts.Add(Loc.P("PCollectionMaps", result.CollectionMaps));

        InfoTitle = Loc.T("UndoDoneTitle");
        InfoText = parts.Count == 0 ? Loc.T("UndoNothing") : Loc.F("UndoDoneBody", Loc.JoinAnd(parts));
        if (result.Errors.Count > 0) InfoText += "\n\n" + Loc.T("UndoErrors") + "\n" + string.Join("\n", result.Errors.Take(8));
        LastCopy = CopyJournal.Load();
        IsInfoOpen = true;

        osuWatch.Start();
        await AnalyzeAsync();
    }

    [RelayCommand]
    private void DismissInfo() => IsInfoOpen = false;

    // ===================================================== captures de test
    // Remplissent l'état sans passer par l'interface (tests/Screens).

    internal void TestSetExporting(MapItemViewModel current, int done, int total)
    {
        CurrentScreen = Screen.Exporting;
        CurrentExport = current;
        exportPhase = ExportPhase.Maps;
        CanStop = true;
        CircleText = Fmt.N(done);
        CircleApproach = (double)done / total;
        lastProgress = new ExportProgress(done, total, current.Item, 1_288_490_189, 3_650_722_201);
        lastEta = TimeSpan.FromSeconds(130);
        RefreshExportTexts();
        foreach (var i in allItems.Where(i => i.IsCopyCandidate).Take(8)) RecentlyExported.Add(i);
    }

    internal void TestShowDone(int n, int scores = 0, int skins = 0)
    {
        var r = new ExportResult { BytesCopied = 3_456_789_012 };
        foreach (var i in allItems.Where(i => i.IsCopyCandidate).Take(n)) r.Exported.Add((i.Item, "x"));
        r.Warnings.Add(new ExportWarning("1011 Artist K - No audio K (x)", false, "audio.mp3"));

        ScoreExportResult? sr = null;
        if (scores > 0)
        {
            sr = new ScoreExportResult { BackupPath = "scores.db.avant-exotique-20261030-140512" };
            foreach (var s in allScores.Where(s => s.IsCopyCandidate).Take(scores)) sr.Added.Add(s.Item);
        }

        SkinExportResult? kr = null;
        if (skins > 0)
        {
            kr = new SkinExportResult();
            foreach (var k in allSkins.Where(k => k.IsCopyCandidate).Take(skins)) kr.Exported.Add((k.Item, "x"));
        }

        ShowDone(new DoneState(r, new CollectionsExportResult(3, 2, 140, "collection.db.avant-exotique-20261029-010203", "collection.db"),
            null, n, sr, null, scores, kr, skins, TimeSpan.FromSeconds(162)));
    }

    internal void TestOpenConfirm() => AskExport();

    internal void TestSetOsu(bool running, bool failed)
    {
        osuWatch.Stop();
        osuInstances = running ? [new OsuInstance(1, OsuKind.Lazer), new OsuInstance(2, OsuKind.Stable)] : [];
        OsuRunning = running;
        StableRunning = running;
        OsuRunningText = OsuProcesses.Describe(osuInstances);
        OsuCloseFailed = failed;
    }

    internal void TestUseLocations(LazerLocation? l, StableLocation? s)
    {
        lazer = l;
        stable = s;
        RefreshLocations();
    }

    internal void TestSetScreen(Screen screen)
    {
        CurrentScreen = screen;
        if (screen == Screen.Analyzing)
        {
            LazerStep.Start(() => "");
            LazerStep.Finish(() => $"{Loc.P("PMaps", 6431)}, {Loc.P("PScores", 2874)}");
            SongsStep.Start(() => "");
            SongsStep.Fraction = 0.58;
            SongsStep.SetDetail(() => Loc.F("StepDiffProgress", Fmt.N(12480), Fmt.N(21354)));
            CompareStep.Reset();
            SetAnalysisProgress(0.3 + 0.6 * 0.58);
        }
    }

    internal void TestSetLanguage(Lang lang) => SetLanguage(lang);

    internal void TestSetSection(Section section) => CurrentSection = section;

    /// <summary>Attend que la sélection (mise à jour en différé) soit recalculée.</summary>
    internal void TestFlushSelection()
    {
        refreshQueued = false;
        RefreshSelection();
    }

    internal IReadOnlyList<ScoreItemViewModel> TestScores => allScores;

    internal IReadOnlyList<SkinItemViewModel> TestSkins => allSkins;

    internal void TestOpenUndo() => AskUndo();

    internal void TestSetLastCopy(CopyJournal? journal) => LastCopy = journal;
}

public enum StepState { Pending, Running, Done }

/// <summary>Une étape de l'analyse affichée sous l'anneau de progression.</summary>
public sealed partial class AnalysisStep(string titleKey) : ObservableObject
{
    private Func<string> detailFactory = () => "";

    public string Title => Loc.T(titleKey);

    [ObservableProperty] private string detail = "";
    [ObservableProperty] private double fraction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(IsRunning), nameof(IsDone))]
    private StepState state;

    public bool IsPending => State == StepState.Pending;
    public bool IsRunning => State == StepState.Running;
    public bool IsDone => State == StepState.Done;

    public void Reset()
    {
        State = StepState.Pending;
        SetDetail(() => "");
        Fraction = 0;
    }

    public void Start(Func<string> detail)
    {
        State = StepState.Running;
        SetDetail(detail);
        Fraction = 0;
    }

    public void Finish(Func<string> detail)
    {
        Fraction = 1;
        SetDetail(detail);
        State = StepState.Done;
    }

    /// <summary>Le détail est gardé sous forme de fonction pour pouvoir le retraduire.</summary>
    public void SetDetail(Func<string> factory)
    {
        detailFactory = factory;
        Detail = factory();
    }

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(Title));
        Detail = detailFactory();
    }
}
