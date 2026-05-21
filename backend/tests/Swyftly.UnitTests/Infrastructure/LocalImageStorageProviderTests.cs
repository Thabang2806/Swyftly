using Microsoft.Extensions.Options;
using Swyftly.Application.Abstractions;
using Swyftly.Infrastructure.Storage;

namespace Swyftly.UnitTests.Infrastructure;

public class LocalImageStorageProviderTests
{
    [Fact]
    public async Task UploadAsync_StoresSupportedImageAndReturnsPublicUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), $"swyftly-image-tests-{Guid.NewGuid():N}");
        var provider = new LocalImageStorageProvider(Options.Create(new ImageStorageOptions
        {
            LocalRootPath = root,
            PublicBasePath = "/media/product-images"
        }));

        try
        {
            await using var stream = new MemoryStream(CreateTinyPngBytes());
            var reference = await provider.UploadAsync(new UploadImageStorageRequest(
                stream,
                "dress.png",
                "image/png",
                stream.Length,
                "seller-one/product-one"));

            Assert.StartsWith("seller-one/product-one/", reference.StorageKey, StringComparison.Ordinal);
            Assert.EndsWith(".png", reference.StorageKey, StringComparison.Ordinal);
            Assert.StartsWith("/media/product-images/seller-one/product-one/", reference.Url, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(root, reference.StorageKey.Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UploadAsync_RejectsUnsupportedContentType()
    {
        var provider = new LocalImageStorageProvider(Options.Create(new ImageStorageOptions
        {
            LocalRootPath = Path.Combine(Path.GetTempPath(), $"swyftly-image-tests-{Guid.NewGuid():N}")
        }));

        await using var stream = new MemoryStream([0x01, 0x02, 0x03]);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.UploadAsync(new UploadImageStorageRequest(
            stream,
            "dress.gif",
            "image/gif",
            stream.Length,
            "seller-one/product-one")));
    }

    private static byte[] CreateTinyPngBytes() =>
    [
        0x89, 0x50, 0x4E, 0x47,
        0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D
    ];
}
