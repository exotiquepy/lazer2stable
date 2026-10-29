using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Exotique.App.ViewModels;

namespace Exotique.App;

public partial class MainWindow : Window, IShell
{
    private readonly MainViewModel vm;

    public MainWindow() : this(new MainViewModel(), autoStart: true)
    {
    }

    internal MainWindow(MainViewModel viewModel, bool autoStart)
    {
        InitializeComponent();
        vm = viewModel;
        vm.Shell = this;
        DataContext = vm;

        Opened += (_, _) => FitToScreen();
        if (autoStart)
            Opened += async (_, _) => await vm.StartAsync();
    }

    /// <summary>Sur un petit écran (portable, zoom Windows à 150 %), la fenêtre est réduite pour tenir dedans.</summary>
    private void FitToScreen()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen == null) return;

        double scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var area = screen.WorkingArea;
        double maxW = area.Width / scale - 24;
        double maxH = area.Height / scale - 24;
        if (Width <= maxW && Height <= maxH) return;

        MinWidth = Math.Min(MinWidth, maxW);
        MinHeight = Math.Min(MinHeight, maxH);
        Width = Math.Min(Width, maxW);
        Height = Math.Min(Height, maxH);
        Position = new PixelPoint(
            area.X + (int)((area.Width - Width * scale) / 2),
            area.Y + (int)((area.Height - Height * scale) / 2));
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    /// <summary>Un clic n'importe où sur la ligne coche ou décoche la map.</summary>
    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: MapItemViewModel item } || !item.IsCopyCandidate) return;

        // Le clic sur la case elle-même est déjà géré par la case.
        if (e.Source is Visual v && v.FindAncestorOfType<CheckBox>(includeSelf: true) != null) return;

        item.IsSelected = !item.IsSelected;
    }

    /// <summary>Espace coche ou décoche la map sous le curseur clavier.</summary>
    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || sender is not ListBox { SelectedItem: MapItemViewModel item } || !item.IsCopyCandidate) return;
        item.IsSelected = !item.IsSelected;
        e.Handled = true;
    }
}
