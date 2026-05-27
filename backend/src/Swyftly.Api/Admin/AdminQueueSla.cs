using Swyftly.Domain.Admin;

namespace Swyftly.Api.Admin;

public static class AdminQueueSla
{
    public const string OnTrack = "OnTrack";
    public const string DueSoon = "DueSoon";
    public const string Overdue = "Overdue";

    public static AdminQueueSlaResponse Calculate(AdminQueueItemType itemType, DateTimeOffset? submittedAtUtc, DateTimeOffset updatedAtUtc, DateTimeOffset now)
    {
        var startedAtUtc = submittedAtUtc ?? updatedAtUtc;
        var thresholdHours = GetThresholdHours(itemType);
        var dueAtUtc = startedAtUtc.AddHours(thresholdHours);
        var elapsed = now - startedAtUtc;
        var ageHours = Math.Max(0, (int)Math.Floor(elapsed.TotalHours));
        var dueSoonAtUtc = startedAtUtc.AddHours(thresholdHours * 0.75);
        var status = now >= dueAtUtc
            ? Overdue
            : now >= dueSoonAtUtc
                ? DueSoon
                : OnTrack;

        return new AdminQueueSlaResponse(ageHours, status, dueAtUtc);
    }

    public static bool Matches(AdminQueueSlaResponse sla, string? requestedSla) =>
        string.IsNullOrWhiteSpace(requestedSla)
        || string.Equals(sla.SlaStatus, requestedSla.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool IsKnownStatus(string? requestedSla) =>
        string.IsNullOrWhiteSpace(requestedSla)
        || string.Equals(requestedSla, OnTrack, StringComparison.OrdinalIgnoreCase)
        || string.Equals(requestedSla, DueSoon, StringComparison.OrdinalIgnoreCase)
        || string.Equals(requestedSla, Overdue, StringComparison.OrdinalIgnoreCase);

    private static int GetThresholdHours(AdminQueueItemType itemType) =>
        itemType switch
        {
            AdminQueueItemType.Seller => 48,
            AdminQueueItemType.Product => 24,
            AdminQueueItemType.ListingRevision => 24,
            AdminQueueItemType.VariantRevision => 24,
            AdminQueueItemType.AdCampaign => 24,
            _ => 24
        };
}

public sealed record AdminQueueSlaResponse(int AgeHours, string SlaStatus, DateTimeOffset SlaDueAtUtc);
