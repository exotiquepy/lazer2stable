using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Exotique.App.ViewModels;

/// <summary>
/// Charge les images de fond en arrière-plan (3 à la fois), réduites à la taille de la vignette,
/// et garde au plus quelques centaines de vignettes en mémoire.
/// </summary>
internal static class ThumbnailLoader
{
    private const int DecodeWidth = 192;
    private const int MaxLoaded = 400;

    private static readonly SemaphoreSlim Gate = new(3);
    private static readonly LinkedList<MapItemViewModel> Loaded = new();
    private static readonly object Lock = new();

    public static void Request(MapItemViewModel item, string path)
    {
        _ = Task.Run(async () =>
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            Bitmap? bitmap = null;
            try
            {
                if (File.Exists(path))
                {
                    await using var stream = File.OpenRead(path);
                    bitmap = Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.MediumQuality);
                }
            }
            catch
            {
                // image illisible ou format non géré : la ligne garde son fond par défaut
            }
            finally
            {
                Gate.Release();
            }

            if (bitmap == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                item.SetThumbnail(bitmap);
                MapItemViewModel? evict = null;
                lock (Lock)
                {
                    Loaded.AddLast(item);
                    if (Loaded.Count > MaxLoaded)
                    {
                        evict = Loaded.First!.Value;
                        Loaded.RemoveFirst();
                    }
                }

                evict?.ReleaseThumbnail();
            });
        });
    }
}
