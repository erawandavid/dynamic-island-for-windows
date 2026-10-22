using Pulau.Core.Media;

namespace Pulau.Core.Tests.Media;

public class MediaSourceNamesTests
{
    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify")]
    [InlineData("MSEdge", "Edge")]
    [InlineData("Chrome", "Chrome")]
    [InlineData("chrome.exe", "Chrome")]
    [InlineData("Chrome.UserData.Profile1", "Chrome")]
    [InlineData("308046B0AF4A39CB", "Firefox")]
    [InlineData(@"C:\Program Files\VideoLAN\VLC\vlc.exe", "VLC")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
    [InlineData("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "Apple Music")]
    public void RecognizesCommonApps(string appId, string expected)
    {
        Assert.Equal(expected, MediaSourceNames.FromAppId(appId));
    }

    [Theory]
    [InlineData("someplayer.exe", "Someplayer")]
    [InlineData("Contoso.Music_abc123!MusicApp", "MusicApp")]
    [InlineData("/usr/bin/player", "Player")]
    public void FallsBackToAReadableName(string appId, string expected)
    {
        Assert.Equal(expected, MediaSourceNames.FromAppId(appId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".exe")]
    public void UsesGenericNameWhenThereIsNothingToShow(string? appId)
    {
        Assert.Equal("Media", MediaSourceNames.FromAppId(appId));
    }
}
