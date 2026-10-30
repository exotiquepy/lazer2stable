using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Exotique.Core;

namespace Exotique.App.ViewModels;

/// <summary>Une ligne de la liste des skins, avec un aperçu : un cercle à la couleur du premier combo, son chiffre, et le curseur.</summary>
public sealed partial class SkinItemViewModel : ObservableObject
{
    private bool previewRequested;

    public SkinItemViewModel(SkinPlanItem item)
    {
        Item = item;
        isSelected = item.Decision == ScoreDecision.Copy;
        SearchText = $"{item.Skin.Name}\n{item.Skin.Creator}\n{item.FolderName}".ToLowerInvariant();
    }

    public SkinPlanItem Item { get; }
    public string SearchText { get; }
    public int FileCount => Item.Skin.Files.Count;

    public bool IsCopyCandidate => Item.Decision == ScoreDecision.Copy;
    public bool IsPresent => Item.Decision == ScoreDecision.AlreadyPresent;
    public bool IsIgnored => Item.Decision == ScoreDecision.Ignored;

    [ObservableProperty] private bool isSelected;

    public string Title => Item.Skin.DisplayName.Length > 0 ? Item.Skin.DisplayName : Loc.T("NoTitle");

    public string SubLine
    {
        get
        {
            var parts = new List<string>();
            if (Item.Skin.Creator.Length > 0 && !Item.Skin.Creator.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                parts.Add(Loc.F("SkinBy", Item.Skin.Creator));
            parts.Add(Loc.P("PFiles", FileCount));
            return string.Join("  ·  ", parts);
        }
    }

    public string FolderText => IsIgnored ? "" : Loc.F("SkinFolder", Item.FolderName);
    public string ReasonText => Item.Reason;
    public string SizeText => Fmt.Bytes(Item.Bytes);

    internal void RefreshTexts()
    {
        foreach (string name in new[] { nameof(Title), nameof(SubLine), nameof(FolderText), nameof(ReasonText), nameof(SizeText) })
            OnPropertyChanged(name);
    }

    // ------------------------------------------------------------- aperçu

    private Bitmap? circle, overlay, number, cursor;

    /// <summary>Chargé à la demande, quand la ligne devient visible.</summary>
    public Bitmap? Circle
    {
        get
        {
            RequestPreview();
            return circle;
        }
    }

    public Bitmap? Overlay => overlay;
    public Bitmap? Number => number;
    public Bitmap? Cursor => cursor;

    private void RequestPreview()
    {
        if (previewRequested || Item.Preview is not { } p) return;
        previewRequested = true;

        _ = Task.Run(() =>
        {
            var c = Load(p.Circle, 104);
            var tinted = c == null ? null : Tint(c, p.Combo);
            if (tinted != null) c!.Dispose();
            var o = Load(p.Overlay, 104);
            var n = Load(p.Number, 40);
            var cur = Load(p.Cursor, 64);

            Dispatcher.UIThread.Post(() =>
            {
                circle = tinted ?? c;
                overlay = o;
                number = n;
                cursor = cur;
                OnPropertyChanged(nameof(Circle));
                OnPropertyChanged(nameof(Overlay));
                OnPropertyChanged(nameof(Number));
                OnPropertyChanged(nameof(Cursor));
            });
        });
    }

    private static Bitmap? Load(string? path, int width)
    {
        if (path == null) return null;
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            var bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width <= width) return bitmap;
            var scaled = bitmap.CreateScaledBitmap(new PixelSize(width, Math.Max(1, bitmap.PixelSize.Height * width / bitmap.PixelSize.Width)));
            bitmap.Dispose();
            return scaled;
        }
        catch
        {
            return null; // image abîmée ou format non géré : l'aperçu s'en passe
        }
    }

    /// <summary>Comme stable : le cercle (souvent blanc) est multiplié par la couleur du combo.</summary>
    private static Bitmap? Tint(Bitmap source, (byte R, byte G, byte B) color)
    {
        try
        {
            var size = source.PixelSize;
            var result = new WriteableBitmap(size, source.Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var fb = result.Lock())
            {
                source.CopyPixels(fb, AlphaFormat.Premul);
                int length = fb.RowBytes * size.Height;
                var pixels = new byte[length];
                Marshal.Copy(fb.Address, pixels, 0, length);
                for (int y = 0; y < size.Height; y++)
                {
                    int row = y * fb.RowBytes;
                    for (int x = 0; x < size.Width; x++)
                    {
                        int i = row + x * 4;
                        pixels[i] = (byte)(pixels[i] * color.B / 255);
                        pixels[i + 1] = (byte)(pixels[i + 1] * color.G / 255);
                        pixels[i + 2] = (byte)(pixels[i + 2] * color.R / 255);
                    }
                }

                Marshal.Copy(pixels, 0, fb.Address, length);
            }

            return result;
        }
        catch
        {
            return null;
        }
    }
}
