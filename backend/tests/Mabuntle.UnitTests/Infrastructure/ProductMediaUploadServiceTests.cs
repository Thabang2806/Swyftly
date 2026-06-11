using Microsoft.Extensions.Options;
using SkiaSharp;
using Mabuntle.Application.Media;
using Mabuntle.Domain.Media;
using Mabuntle.Infrastructure.Storage;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class ProductMediaUploadServiceTests
{
    [Fact]
    public async Task UploadAsync_CreatesOriginalAssetAndThreeWebpVariants()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mabuntle-media-tests-{Guid.NewGuid():N}");
        var options = Options.Create(new ImageStorageOptions
        {
            LocalRootPath = root,
            PublicBasePath = "/media/product-images"
        });
        var service = new ProductMediaUploadService(
            new LocalImageStorageProvider(options),
            new TrustLocalCleanMediaMalwareScanner(),
            new MediaImageProcessor(options),
            options,
            TimeProvider.System);

        try
        {
            var sellerId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            await using var stream = new MemoryStream(CreateTinyPngBytes());

            var result = await service.UploadAsync(new ProductMediaUploadRequest(
                stream,
                "dress.png",
                "image/png",
                stream.Length,
                "seller-one/product-one",
                sellerId,
                productId,
                null));

            Assert.Equal(sellerId, result.Asset.SellerId);
            Assert.Equal(productId, result.Asset.ProductId);
            Assert.Equal(MediaScanStatus.Clean, result.Asset.ScanStatus);
            Assert.Equal(3, result.Variants.Count);
            Assert.All(result.Variants, variant =>
            {
                Assert.Equal("image/webp", variant.ContentType);
                Assert.EndsWith(".webp", variant.StorageKey, StringComparison.Ordinal);
                Assert.True(File.Exists(Path.Combine(root, variant.StorageKey.Replace('/', Path.DirectorySeparatorChar))));
            });
            Assert.Equal(MediaAssetVariantKind.Detail, result.DetailVariant.Kind);
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
    public async Task UploadAsync_RejectsScannerFailureWithoutPersistingObjects()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mabuntle-media-tests-{Guid.NewGuid():N}");
        var options = Options.Create(new ImageStorageOptions
        {
            LocalRootPath = root,
            PublicBasePath = "/media/product-images"
        });
        var service = new ProductMediaUploadService(
            new LocalImageStorageProvider(options),
            new RejectingScanner(),
            new MediaImageProcessor(options),
            options,
            TimeProvider.System);

        try
        {
            await using var stream = new MemoryStream(CreateTinyPngBytes());

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(new ProductMediaUploadRequest(
                stream,
                "dress.png",
                "image/png",
                stream.Length,
                "seller-one/product-one",
                Guid.NewGuid(),
                Guid.NewGuid(),
                null)));

            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class RejectingScanner : IMediaMalwareScanner
    {
        public Task<MediaScanResult> ScanAsync(
            MediaScanRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MediaScanResult.Rejected("test", "Rejected by scanner."));
    }

    private static byte[] CreateTinyPngBytes()
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Plum);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
