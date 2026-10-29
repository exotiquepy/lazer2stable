using System.Diagnostics;

namespace Exotique.Core;

public enum OsuKind { Stable, Lazer }

public sealed record OsuInstance(int Id, OsuKind Kind);

/// <summary>
/// Repère osu! stable et osu! lazer (tous deux nommés « osu! ») et sait les fermer :
/// d'abord proprement, comme un clic sur la croix, puis de force seulement si on le demande.
/// </summary>
public static class OsuProcesses
{
    private const string ProcessName = "osu!";

    public static List<OsuInstance> Running()
    {
        var list = new List<OsuInstance>();
        Process[] procs;
        try
        {
            procs = Process.GetProcessesByName(ProcessName);
        }
        catch
        {
            return list;
        }

        foreach (var p in procs)
        {
            using (p)
                list.Add(new OsuInstance(p.Id, KindOf(p)));
        }

        return list;
    }

    /// <summary>« osu! lazer est ouvert », « osu! lazer et osu! stable sont ouverts »… dans la langue en cours.</summary>
    public static string Describe(IReadOnlyCollection<OsuInstance> running)
    {
        bool lazer = running.Any(r => r.Kind == OsuKind.Lazer);
        bool stable = running.Any(r => r.Kind == OsuKind.Stable);
        return (lazer, stable) switch
        {
            (true, true) => Loc.T("OsuBoth"),
            (true, false) => Loc.T("OsuLazer"),
            (false, true) => Loc.T("OsuStable"),
            _ => "",
        };
    }

    /// <summary>Demande à chaque fenêtre osu! de se fermer, puis attend. Renvoie ce qui tourne encore.</summary>
    public static async Task<List<OsuInstance>> CloseGracefullyAsync(TimeSpan timeout)
    {
        Process[] procs;
        try
        {
            procs = Process.GetProcessesByName(ProcessName);
        }
        catch
        {
            return Running();
        }

        try
        {
            foreach (var p in procs)
            {
                try { p.CloseMainWindow(); }
                catch { /* déjà fermé */ }
            }

            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && !procs.All(HasExited))
                await Task.Delay(250).ConfigureAwait(false);
        }
        finally
        {
            foreach (var p in procs) p.Dispose();
        }

        return Running();
    }

    /// <summary>Fermeture forcée (perd ce qu'osu! n'a pas encore enregistré). Renvoie ce qui tourne encore.</summary>
    public static List<OsuInstance> Kill()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(ProcessName))
            {
                using (p)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(5000);
                    }
                    catch
                    {
                        // accès refusé ou déjà fermé
                    }
                }
            }
        }
        catch
        {
            // liste des processus inaccessible
        }

        return Running();
    }

    private static OsuKind KindOf(Process p)
    {
        // lazer est installé dans %LOCALAPPDATA%\osulazer ; stable, dans son propre dossier.
        try
        {
            string? path = p.MainModule?.FileName;
            if (path != null && path.Contains("osulazer", StringComparison.OrdinalIgnoreCase))
                return OsuKind.Lazer;
        }
        catch
        {
            // chemin illisible : on suppose stable
        }

        return OsuKind.Stable;
    }

    private static bool HasExited(Process p)
    {
        try { return p.HasExited; }
        catch { return true; }
    }
}
