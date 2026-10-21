using Pulau.Core;

namespace Pulau.Core.Tests;

public class ProductInfoTests
{
    [Fact]
    public void Name_IsUsableAsFolderName()
    {
        Assert.False(string.IsNullOrWhiteSpace(ProductInfo.Name));
        Assert.Equal(-1, ProductInfo.Name.IndexOfAny(Path.GetInvalidFileNameChars()));
    }

    [Fact]
    public void RepositoryUrl_IsAbsoluteHttps()
    {
        Assert.True(Uri.TryCreate(ProductInfo.RepositoryUrl, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }
}
