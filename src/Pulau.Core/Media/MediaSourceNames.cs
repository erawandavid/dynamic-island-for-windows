namespace Pulau.Core.Media;

/// <summary>Turns a media session's app id (AppUserModelID) into a short, readable app name.</summary>
public static class MediaSourceNames
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spotify"] = "Spotify",
        ["SpotifyAB.SpotifyMusic"] = "Spotify",
        ["chrome"] = "Chrome",
        ["msedge"] = "Edge",
        ["firefox"] = "Firefox",
        ["308046B0AF4A39CB"] = "Firefox",
        ["6F193CCC56814779"] = "Firefox",
        ["brave"] = "Brave",
        ["opera"] = "Opera",
        ["vivaldi"] = "Vivaldi",
        ["vlc"] = "VLC",
        ["Microsoft.ZuneMusic"] = "Media Player",
        ["Microsoft.ZuneVideo"] = "Movies & TV",
        ["Microsoft.Media.Player"] = "Media Player",
        ["AppleInc.AppleMusicWin"] = "Apple Music",
        ["AppleInc.iTunes"] = "iTunes",
        ["itunes"] = "iTunes",
        ["deezer"] = "Deezer",
        ["tidal"] = "TIDAL",
        ["foobar2000"] = "foobar2000",
        ["musicbee"] = "MusicBee",
        ["aimp"] = "AIMP",
        ["winamp"] = "Winamp",
        ["mpc-hc64"] = "MPC-HC",
        ["mpc-hc"] = "MPC-HC",
        ["PotPlayerMini64"] = "PotPlayer",
    };

    /// <summary>
    /// Examples: <c>Spotify.exe</c> → Spotify, <c>MSEdge</c> → Edge,
    /// <c>SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify</c> → Spotify, <c>C:\Apps\vlc.exe</c> → VLC.
    /// </summary>
    public static string FromAppId(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return "Media";
        }

        var id = appId.Trim();

        // Packaged app: "<PackageName>_<PublisherHash>!<AppId>".
        var bang = id.IndexOf('!', StringComparison.Ordinal);
        if (bang >= 0)
        {
            var family = id[..bang];
            var underscore = family.LastIndexOf('_');
            var packageName = underscore > 0 ? family[..underscore] : family;
            if (TryKnown(packageName, out var packaged))
            {
                return packaged;
            }

            id = id[(bang + 1)..];
        }

        // Path or file name: keep the file name without ".exe".
        var slash = id.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
        {
            id = id[(slash + 1)..];
        }

        if (id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            id = id[..^4];
        }

        if (TryKnown(id, out var name))
        {
            return name;
        }

        if (id.Length == 0)
        {
            return "Media";
        }

        return char.ToUpperInvariant(id[0]) + id[1..];
    }

    private static bool TryKnown(string id, out string name)
    {
        if (Known.TryGetValue(id, out var exact))
        {
            name = exact;
            return true;
        }

        // Browser profiles and installed web apps, e.g. "Chrome.UserData.Profile1" or "Brave._crx_abc".
        foreach (var (key, value) in Known)
        {
            if (id.Length > key.Length && id.StartsWith(key, StringComparison.OrdinalIgnoreCase) && id[key.Length] is '.' or '_')
            {
                name = value;
                return true;
            }
        }

        name = string.Empty;
        return false;
    }
}
