using Pulau.Core.Apps;

namespace Pulau.Core.Tests.Apps;

public class AppIdMatcherTests
{
    private static ProcessIdentity Desktop(string path) => new(path, null, null);

    [Theory]
    [InlineData("Spotify.exe", @"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe")]
    [InlineData("spotify.exe", @"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe")]
    [InlineData("Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe")]
    [InlineData("Chrome.UserData.Profile1", @"C:\Program Files\Google\Chrome\Application\chrome.exe")]
    [InlineData("MSEdge", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe")]
    [InlineData("308046B0AF4A39CB", @"C:\Program Files\Mozilla Firefox\firefox.exe")]
    [InlineData("Brave", @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe")]
    [InlineData(@"C:\Program Files\VideoLAN\VLC\vlc.exe", @"C:\Program Files\VideoLAN\VLC\vlc.exe")]
    public void MatchesDesktopApps(string appId, string imagePath)
    {
        Assert.True(AppIdMatcher.Matches(appId, Desktop(imagePath)));
    }

    [Theory]
    [InlineData("Spotify.exe", @"C:\Program Files\Google\Chrome\Application\chrome.exe")]
    [InlineData("Chrome", @"C:\Program Files\Mozilla Firefox\firefox.exe")]
    [InlineData("MSEdge", @"C:\Program Files\Google\Chrome\Application\chrome.exe")]
    [InlineData("Chrome", @"C:\Tools\chromedriver.exe")]
    public void DoesNotMatchOtherDesktopApps(string appId, string imagePath)
    {
        Assert.False(AppIdMatcher.Matches(appId, Desktop(imagePath)));
    }

    [Fact]
    public void MatchesPackagedApps_ByAppUserModelId()
    {
        var store = new ProcessIdentity(
            @"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_1.2.3.0_x64__zpdnekdrzrea0\Spotify.exe",
            "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
            "SpotifyAB.SpotifyMusic_zpdnekdrzrea0");

        Assert.True(AppIdMatcher.Matches("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", store));
    }

    [Fact]
    public void MatchesPackagedApps_ByPackageFamily()
    {
        var player = new ProcessIdentity(
            @"C:\Program Files\WindowsApps\Microsoft.ZuneMusic_11.0.0.0_x64__8wekyb3d8bbwe\Microsoft.Media.Player.exe",
            null,
            "Microsoft.ZuneMusic_8wekyb3d8bbwe");

        Assert.True(AppIdMatcher.Matches("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", player));
    }

    [Fact]
    public void PackagedId_DoesNotMatchUnpackagedProcessWithTheSameExecutable()
    {
        var desktopSpotify = Desktop(@"C:\Users\me\AppData\Roaming\Spotify\Spotify.exe");

        Assert.False(AppIdMatcher.Matches("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", desktopSpotify));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyIds_NeverMatch(string? appId)
    {
        Assert.False(AppIdMatcher.Matches(appId, Desktop(@"C:\x\Spotify.exe")));
    }

    [Fact]
    public void ProcessesWithoutKnownImage_NeverMatchDesktopIds()
    {
        Assert.False(AppIdMatcher.Matches("Spotify.exe", default));
    }
}
