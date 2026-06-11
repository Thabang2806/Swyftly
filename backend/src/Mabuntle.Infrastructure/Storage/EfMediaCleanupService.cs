using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mabuntle.Application.Media;
using Mabuntle.Domain.Media;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.Infrastructure.Storage;

public sealed class EfMediaCleanupService(
    MabuntleDbContext dbContext,
    IProductMediaUploadService mediaUploadService,
    IOptions<MediaCleanupOptions> options) : IMediaCleanupService
{
    private readonly MediaCleanupOptions options = options.Value;

    public async Task<MediaCleanupResult> CleanupAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var cutoff = now.AddHours(-Math.Max(1, options.GracePeriodHours));
        var batchSize = Math.Clamp(options.BatchSize, 1, 500);
        var candidates = await dbContext.MediaAssets
            .Where(asset =>
                (asset.LifecycleStatus == MediaAssetLifecycleStatus.PendingDeletion ||
                 asset.LifecycleStatus == MediaAssetLifecycleStatus.DeleteFailed) &&
                asset.DeleteRequestedAtUtc <= cutoff)
            .OrderBy(asset => asset.DeleteRequestedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        var remainingSlots = batchSize - candidates.Count;
        if (remainingSlots > 0)
        {
            var orphaned = await dbContext.MediaAssets
                .Where(asset =>
                    asset.LifecycleStatus == MediaAssetLifecycleStatus.Stored &&
                    asset.CreatedAtUtc <= cutoff &&
                    !dbContext.ProductImages.Any(image => image.MediaAssetId == asset.Id) &&
                    !dbContext.ProductListingRevisionImages.Any(image => image.MediaAssetId == asset.Id))
                .OrderBy(asset => asset.CreatedAtUtc)
                .Take(remainingSlots)
                .ToListAsync(cancellationToken);
            candidates.AddRange(orphaned);
        }

        var deleted = 0;
        var failed = 0;
        foreach (var asset in candidates)
        {
            var variants = await dbContext.MediaAssetVariants
                .Where(variant => variant.MediaAssetId == asset.Id)
                .ToListAsync(cancellationToken);

            await mediaUploadService.DeleteAsync(asset, variants, now, cancellationToken);
            if (asset.LifecycleStatus == MediaAssetLifecycleStatus.Deleted)
            {
                deleted++;
            }
            else
            {
                failed++;
            }
        }

        if (candidates.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new MediaCleanupResult(candidates.Count, deleted, failed);
    }
}
