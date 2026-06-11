using Microsoft.EntityFrameworkCore;
using Mabuntle.Application.Identity;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Admin;

public static class AdminAuditLogEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapAdminAuditLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/audit-logs")
            .WithTags("Admin Audit Logs")
            .RequireAuthorization(MabuntlePolicies.AdminOnly);

        group.MapGet("", SearchAsync)
            .WithName("SearchAdminAuditLogs")
            .WithSummary("Returns filtered admin audit logs for sensitive actions.")
            .Produces<AdminAuditLogSearchResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        string? actionType,
        string? entityType,
        string? entityId,
        string? actorUserId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? pageNumber,
        int? pageSize,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["dateRange"] = ["fromUtc must be earlier than or equal to toUtc."]
            });
        }

        var page = Math.Max(1, pageNumber ?? 1);
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

        var query = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var normalized = actionType.Trim();
            query = query.Where(log => log.ActionType == normalized);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalized = entityType.Trim();
            query = query.Where(log => log.EntityType == normalized);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            var normalized = entityId.Trim();
            query = query.Where(log => log.EntityId == normalized);
        }

        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            var normalized = actorUserId.Trim();
            query = query.Where(log => log.ActorUserId == normalized);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.CreatedAtUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.CreatedAtUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(log => new AdminAuditLogDetailResponse(
                log.Id,
                log.ActorUserId,
                log.ActorRole,
                log.ActionType,
                log.EntityType,
                log.EntityId,
                log.PreviousValueJson,
                log.NewValueJson,
                log.Reason,
                log.IpAddress,
                log.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(new AdminAuditLogSearchResponse(
            items,
            page,
            size,
            totalCount));
    }
}

public sealed record AdminAuditLogSearchResponse(
    IReadOnlyCollection<AdminAuditLogDetailResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record AdminAuditLogDetailResponse(
    Guid Id,
    string? ActorUserId,
    string? ActorRole,
    string ActionType,
    string EntityType,
    string? EntityId,
    string? PreviousValueJson,
    string? NewValueJson,
    string? Reason,
    string? IpAddress,
    DateTimeOffset CreatedAtUtc);
