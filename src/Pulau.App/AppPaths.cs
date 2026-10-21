using System.IO;
using Pulau.Core;

namespace Pulau;

/// <summary>Per-user folders. Nothing is written outside these.</summary>
internal static class AppPaths
{
    /// <summary><c>%LOCALAPPDATA%\Pulau</c></summary>
    public static string LocalData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductInfo.Name);

    /// <summary><c>%LOCALAPPDATA%\Pulau\logs</c></summary>
    public static string Logs { get; } = Path.Combine(LocalData, "logs");
}
