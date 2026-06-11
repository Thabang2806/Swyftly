using Microsoft.Extensions.Options;
using Mabuntle.Infrastructure.Storage;

namespace Mabuntle.UnitTests.Infrastructure;

public sealed class S3ImageStorageProviderTests
{
    [Fact]
    public async Task CheckReadinessAsync_ReportsMissingS3Configuration()
    {
        var provider = new S3ImageStorageProvider(Options.Create(new ImageStorageOptions
        {
            ProviderName = "S3"
        }));

        var result = await provider.CheckReadinessAsync();

        Assert.False(result.IsReady);
        Assert.Contains("ImageStorage:S3:BucketName", result.Failures.Keys);
        Assert.Contains("ImageStorage:S3:ServiceUrl", result.Failures.Keys);
        Assert.Contains("ImageStorage:S3:AccessKeyId", result.Failures.Keys);
    }

    [Fact]
    public async Task CreateReferenceAsync_RejectsTraversalSegments()
    {
        var provider = new S3ImageStorageProvider(Options.Create(new ImageStorageOptions
        {
            ProviderName = "S3",
            S3 =
            {
                PublicBaseUrl = "https://cdn.example.test/media"
            }
        }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            provider.CreateReferenceAsync(new("seller-one/../product-one/image.webp", null)));
    }
}
