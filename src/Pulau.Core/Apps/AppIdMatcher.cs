namespace Pulau.Core.Apps;

/// <summary>What Pulau can learn about the process that owns a window.</summary>
/// <param name="ImagePath">Full path of the executable, e.g. <c>C:\…\Spotify.exe</c>.</param>
/// <param name="AppUserModelId">For packaged (Store/MSIX) apps, the app's AppUserModelID.</param>
/// <param name="PackageFamilyName">For packaged apps, the package family name.</param>
public readonly record struct ProcessIdentity(string? ImagePath, string? AppUserModelId, string? PackageFamilyName);

/// <summary>
/// Decides whether a process belongs to the app behind a media session. Media sessions only carry an
/// app id (AppUserModelID): <c>Spotify.exe</c>, <c>Chrome</c>, <c>MSEdge</c>,
/// <c>SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify</c>…
/// </summary>
public static class AppIdMatcher
{
    // App ids that don't contain the executable's name.
    private static readonly Dictionary<string, string[]> Executables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MSEdge"] = ["msedge"],
        ["308046B0AF4A39CB"] = ["firefox"],
        ["6F193CCC56814779"] = ["firefox"],
    };

    public static bool Matches(string? appId, ProcessIdentity process)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        var id = appId.Trim();

        // Packaged apps: the exact AppUserModelID, or the package family name before the "!".
        if (!string.IsNullOrEmpty(process.AppUserModelId) && string.Equals(process.AppUserModelId, id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var bang = id.IndexOf('!', StringComparison.Ordinal);
        if (bang >= 0)
        {
            return !string.IsNullOrEmpty(process.PackageFamilyName) &&
                   string.Equals(process.PackageFamilyName, id[..bang], StringComparison.OrdinalIgnoreCase);
        }

        // Desktop apps: the id is the executable ("Spotify.exe"), its name ("Chrome"), a path,
        // or the name followed by a profile suffix ("Chrome.UserData.Profile1").
        var executable = ExecutableName(process.ImagePath);
        if (executable is null)
        {
            return false;
        }

        var idName = ExecutableName(id)!;
        if (string.Equals(idName, executable, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (idName.Length > executable.Length &&
            idName.StartsWith(executable, StringComparison.OrdinalIgnoreCase) &&
            idName[executable.Length] is '.' or '_')
        {
            return true;
        }

        return Executables.TryGetValue(id, out var names) &&
               names.Contains(executable, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>File name without directory and without ".exe"; <c>null</c> for an empty path.</summary>
    private static string? ExecutableName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var name = path.Trim();
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}
