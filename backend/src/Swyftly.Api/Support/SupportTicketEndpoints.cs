using System.Security.Claims;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Swyftly.Api.Admin;
using Microsoft.EntityFrameworkCore;
using Swyftly.Application.Admin;
using Swyftly.Api.Notifications;
using Swyftly.Application.Identity;
using Swyftly.Application.Notifications;
using Swyftly.Domain.Admin;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Sellers;
using Swyftly.Domain.Support;
using Swyftly.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Swyftly.Api.Support;

public static class SupportTicketEndpoints
{
    private const string SupportSavedViewQueue = "Support";

    public static IEndpointRouteBuilder MapSupportTicketEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/buyer/support-tickets")
            .WithTags("Buyer Support Tickets")
            .RequireAuthorization(SwyftlyPolicies.BuyerOnly);

        buyerGroup.MapPost("", CreateBuyerTicketAsync)
            .WithName("CreateBuyerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapGet("", GetBuyerTicketsAsync)
            .WithName("GetBuyerSupportTickets")
            .Produces<IReadOnlyCollection<SupportTicketResponse>>(StatusCodes.Status200OK);

        buyerGroup.MapGet("/{ticketId:guid}", GetBuyerTicketAsync)
            .WithName("GetBuyerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        buyerGroup.MapPost("/{ticketId:guid}/messages", AddBuyerMessageAsync)
            .WithName("AddBuyerSupportMessage")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var sellerGroup = app.MapGroup("/api/seller/support-tickets")
            .WithTags("Seller Support Tickets")
            .RequireAuthorization(SwyftlyPolicies.SellerOnly);

        sellerGroup.MapPost("", CreateSellerTicketAsync)
            .WithName("CreateSellerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapGet("", GetSellerTicketsAsync)
            .WithName("GetSellerSupportTickets")
            .Produces<IReadOnlyCollection<SupportTicketResponse>>(StatusCodes.Status200OK);

        sellerGroup.MapGet("/{ticketId:guid}", GetSellerTicketAsync)
            .WithName("GetSellerSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        sellerGroup.MapPost("/{ticketId:guid}/messages", AddSellerMessageAsync)
            .WithName("AddSellerSupportMessage")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var supportGroup = app.MapGroup("/api/support/tickets")
            .WithTags("Support Tickets")
            .RequireAuthorization(SwyftlyPolicies.SupportAgentOnly);

        supportGroup.MapGet("", GetSupportTicketsAsync)
            .WithName("GetSupportTickets")
            .Produces<IReadOnlyCollection<SupportTicketResponse>>(StatusCodes.Status200OK);

        supportGroup.MapGet("/queue", GetSupportQueueAsync)
            .WithName("GetSupportTicketQueue")
            .Produces<SupportTicketQueueResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        supportGroup.MapGet("/queue/export.csv", ExportSupportQueueCsvAsync)
            .WithName("ExportSupportTicketQueueCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem();

        supportGroup.MapGet("/summary", GetSupportSummaryAsync)
            .WithName("GetSupportTicketSummary")
            .Produces<SupportTicketSummaryResponse>(StatusCodes.Status200OK);

        supportGroup.MapGet("/quality-report", GetSupportQualityReportAsync)
            .WithName("GetSupportTicketQualityReport")
            .Produces<SupportTicketQualityReportResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        supportGroup.MapGet("/quality-report/export.csv", ExportSupportQualityReportCsvAsync)
            .WithName("ExportSupportTicketQualityReportCsv")
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .ProducesValidationProblem();

        supportGroup.MapGet("/views", GetSupportSavedViewsAsync)
            .WithName("GetSupportTicketSavedViews")
            .Produces<IReadOnlyCollection<AdminQueueSavedViewResponse>>(StatusCodes.Status200OK);

        supportGroup.MapPost("/views", CreateSupportSavedViewAsync)
            .WithName("CreateSupportTicketSavedView")
            .Produces<AdminQueueSavedViewResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        supportGroup.MapPut("/views/{viewId:guid}", UpdateSupportSavedViewAsync)
            .WithName("UpdateSupportTicketSavedView")
            .Produces<AdminQueueSavedViewResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapDelete("/views/{viewId:guid}", DeleteSupportSavedViewAsync)
            .WithName("DeleteSupportTicketSavedView")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapPost("/views/{viewId:guid}/make-default", MakeDefaultSupportSavedViewAsync)
            .WithName("MakeDefaultSupportTicketSavedView")
            .Produces<AdminQueueSavedViewResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapGet("/{ticketId:guid}", GetSupportTicketAsync)
            .WithName("GetSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapPost("/{ticketId:guid}/claim", ClaimTicketAsync)
            .WithName("ClaimSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapPost("/{ticketId:guid}/unclaim", UnclaimTicketAsync)
            .WithName("UnclaimSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapPut("/{ticketId:guid}/triage", TriageTicketAsync)
            .WithName("TriageSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapPost("/{ticketId:guid}/escalate", EscalateTicketAsync)
            .WithName("EscalateSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        supportGroup.MapPost("/{ticketId:guid}/messages", AddSupportMessageAsync)
            .WithName("AddSupportTicketMessage")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        supportGroup.MapPost("/{ticketId:guid}/internal-notes", AddSupportInternalNoteAsync)
            .WithName("AddSupportTicketInternalNote")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        supportGroup.MapPost("/{ticketId:guid}/resolve", ResolveTicketAsync)
            .WithName("ResolveSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        supportGroup.MapPost("/{ticketId:guid}/close", CloseTicketAsync)
            .WithName("CloseSupportTicket")
            .Produces<SupportTicketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateBuyerTicketAsync(
        CreateSupportTicketRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        if (!TryParseCategory(request.Category, out var category))
        {
            return InvalidCategory();
        }

        var validation = await ValidateBuyerLinksAsync(request, buyer.Id, dbContext, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var ticket = new SupportTicket(
            userId,
            SwyftlyRoles.Buyer,
            buyer.Id,
            null,
            category,
            request.Subject,
            request.Description,
            request.LinkedOrderId,
            request.LinkedProductId,
            request.LinkedSellerId,
            request.LinkedPaymentId,
            timeProvider.GetUtcNow());

        dbContext.SupportTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> CreateSellerTicketAsync(
        CreateSupportTicketRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        if (!TryParseCategory(request.Category, out var category))
        {
            return InvalidCategory();
        }

        var validation = await ValidateSellerLinksAsync(request, seller.Id, dbContext, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var ticket = new SupportTicket(
            userId,
            SwyftlyRoles.Seller,
            null,
            seller.Id,
            category,
            request.Subject,
            request.Description,
            request.LinkedOrderId,
            request.LinkedProductId,
            request.LinkedSellerId,
            request.LinkedPaymentId,
            timeProvider.GetUtcNow());

        dbContext.SupportTickets.Add(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        return HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> GetBuyerTicketsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var tickets = await TicketQuery(dbContext)
            .Where(ticket => ticket.BuyerId == buyer.Id)
            .OrderByDescending(ticket => ticket.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(tickets.Select(ticket => Map(ticket, includeInternalMessages: false)).ToArray());
    }

    private static async Task<IResult> GetSellerTicketsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var tickets = await TicketQuery(dbContext)
            .Where(ticket => ticket.SellerId == seller.Id)
            .OrderByDescending(ticket => ticket.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(tickets.Select(ticket => Map(ticket, includeInternalMessages: false)).ToArray());
    }

    private static async Task<IResult> GetBuyerTicketAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var ticket = await TicketQuery(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && ticket.BuyerId == buyer.Id, cancellationToken);

        return ticket is null
            ? TicketNotFound()
            : HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> GetSellerTicketAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        var ticket = await TicketQuery(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId && ticket.SellerId == seller.Id, cancellationToken);

        return ticket is null
            ? TicketNotFound()
            : HttpResults.Ok(Map(ticket, includeInternalMessages: false));
    }

    private static async Task<IResult> GetSupportTicketsAsync(
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tickets = await TicketQuery(dbContext)
            .OrderByDescending(ticket => ticket.OpenedAtUtc)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(tickets.Select(ticket => Map(ticket, includeInternalMessages: true)).ToArray());
    }

    private static async Task<IResult> GetSupportTicketAsync(
        Guid ticketId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var ticket = await TicketQuery(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (ticket is null)
        {
            return TicketNotFound();
        }

        var context = await BuildCustomerContextAsync(ticket, dbContext, cancellationToken);
        return HttpResults.Ok(Map(ticket, includeInternalMessages: true, context));
    }

    private static async Task<IResult> GetSupportQueueAsync(
        string? view,
        string? status,
        string? category,
        string? search,
        string? assigned,
        string? priority,
        string? sla,
        Guid? savedViewId,
        int? page,
        int? pageSize,
        string? sort,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var savedView = await GetSupportSavedViewForRequestAsync(savedViewId, principal, dbContext, cancellationToken);
        view = AdminModerationQueueEndpoints.Merge(view, savedView?.View);
        status = AdminModerationQueueEndpoints.Merge(status, savedView?.Status);
        category = AdminModerationQueueEndpoints.Merge(category, savedView?.Category);
        search = AdminModerationQueueEndpoints.Merge(search, savedView?.Search);
        assigned = AdminModerationQueueEndpoints.Merge(assigned, savedView?.Assigned);
        priority = AdminModerationQueueEndpoints.Merge(priority, savedView?.Priority);
        sla = AdminModerationQueueEndpoints.Merge(sla, savedView?.Sla);
        pageSize = AdminModerationQueueEndpoints.Merge(pageSize, savedView?.PageSize);
        sort = AdminModerationQueueEndpoints.Merge(sort, savedView?.Sort);

        if (!TryParseOptionalStatus(status, out var parsedStatus))
        {
            return Validation("status", $"Status must be one of: {string.Join(", ", Enum.GetNames<SupportTicketStatus>())}.");
        }

        if (!TryParseOptionalCategory(category, out var parsedCategory))
        {
            return InvalidCategory();
        }

        if (!TryParseOptionalPriority(priority, out var parsedPriority))
        {
            return Validation("priority", $"Priority must be one of: {string.Join(", ", Enum.GetNames<SupportTicketPriority>())}.");
        }

        if (!IsKnownSla(sla))
        {
            return Validation("sla", "SLA must be one of: OnTrack, DueSoon, Overdue.");
        }

        var normalizedView = string.IsNullOrWhiteSpace(view) ? "NeedsAttention" : view.Trim();
        if (!new[] { "NeedsAttention", "All" }.Contains(normalizedView, StringComparer.OrdinalIgnoreCase))
        {
            return Validation("view", "View must be NeedsAttention or All.");
        }

        var pageNumber = Math.Max(1, page ?? 1);
        var size = Math.Clamp(pageSize ?? 25, 1, 100);
        var now = timeProvider.GetUtcNow();
        var options = ReadSupportOperationsOptions(configuration);

        var tickets = await TicketQuery(dbContext)
            .ToListAsync(cancellationToken);
        var assignedNames = await ReadAssignedUserNamesAsync(tickets, dbContext, cancellationToken);
        var actorUserId = TryGetUserId(principal, out var parsedActorUserId) ? parsedActorUserId : (Guid?)null;
        var normalizedSearch = search?.Trim().ToLowerInvariant();

        var items = tickets
            .Select(ticket => MapQueueItem(ticket, assignedNames, now, options))
            .Where(item => string.Equals(normalizedView, "All", StringComparison.OrdinalIgnoreCase)
                || item.Status is not (nameof(SupportTicketStatus.Resolved) or nameof(SupportTicketStatus.Closed)))
            .Where(item => parsedStatus is null || string.Equals(item.Status, parsedStatus.Value.ToString(), StringComparison.Ordinal))
            .Where(item => parsedCategory is null || string.Equals(item.Category, parsedCategory.Value.ToString(), StringComparison.Ordinal))
            .Where(item => parsedPriority is null || string.Equals(item.Priority, parsedPriority.Value.ToString(), StringComparison.Ordinal))
            .Where(item => MatchesAssignment(item, assigned, actorUserId))
            .Where(item => string.IsNullOrWhiteSpace(sla) || string.Equals(item.SlaStatus, sla.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(normalizedSearch)
                || string.Join(' ', item.SupportTicketId, item.Subject, item.Description, item.CreatedByRole, item.BuyerId, item.SellerId, item.LinkedOrderId, item.LatestInternalNote)
                    .ToLowerInvariant()
                    .Contains(normalizedSearch))
            .ToArray();

        var sorted = SortQueueItems(items, sort).ToArray();
        var pageItems = sorted.Skip((pageNumber - 1) * size).Take(size).ToArray();

        return HttpResults.Ok(new SupportTicketQueueResponse(
            pageItems,
            sorted.Length,
            pageNumber,
            size,
            CountBy(sorted, item => item.Status),
            CountBy(sorted, item => item.Priority),
            CountBy(sorted, item => item.SlaStatus)));
    }

    private static async Task<IResult> GetSupportSummaryAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var options = ReadSupportOperationsOptions(configuration);
        var tickets = await TicketQuery(dbContext).ToListAsync(cancellationToken);
        var assignedNames = await ReadAssignedUserNamesAsync(tickets, dbContext, cancellationToken);
        var items = tickets.Select(ticket => MapQueueItem(ticket, assignedNames, now, options)).ToArray();
        var openItems = items.Where(item => item.Status is not (nameof(SupportTicketStatus.Resolved) or nameof(SupportTicketStatus.Closed))).ToArray();
        var actorUserId = TryGetUserId(principal, out var parsedActorUserId) ? parsedActorUserId : (Guid?)null;
        var resolvedToday = tickets.Count(ticket => ticket.ResolvedAtUtc.HasValue && ticket.ResolvedAtUtc.Value.Date == now.Date);
        var sevenDaysAgo = now.AddDays(-7);
        var resolvedLast7Days = tickets.Count(ticket => ticket.ResolvedAtUtc.HasValue && ticket.ResolvedAtUtc.Value >= sevenDaysAgo);

        return HttpResults.Ok(new SupportTicketSummaryResponse(
            now,
            openItems.Length,
            openItems.Count(item => item.Status == nameof(SupportTicketStatus.Escalated)),
            openItems.Count(item => item.SlaStatus == SupportSlaStatus.Overdue),
            actorUserId.HasValue ? openItems.Count(item => item.AssignedSupportUserId == actorUserId.Value) : 0,
            openItems.Count(item => item.AssignedSupportUserId is null),
            resolvedToday,
            resolvedLast7Days,
            CalculateAverageFirstResponseHours(tickets),
            CalculateAverageResolutionHours(tickets),
            CountBy(openItems, item => item.Status),
            CountBy(openItems, item => item.Priority),
            CountBy(openItems, item => item.SlaStatus),
            openItems
                .GroupBy(item => item.AssignedSupportUserId?.ToString() ?? "Unassigned")
                .Select(group => new SupportTicketAssigneeCountResponse(group.Key, group.First().AssignedSupportDisplayName, group.Count()))
                .OrderBy(item => item.AssignedSupportDisplayName ?? item.AssignedSupportUserId)
                .ToArray()));
    }

    private static async Task<IResult> ExportSupportQueueCsvAsync(
        string? view,
        string? status,
        string? category,
        string? search,
        string? assigned,
        string? priority,
        string? sla,
        Guid? savedViewId,
        string? sort,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var savedView = await GetSupportSavedViewForRequestAsync(savedViewId, principal, dbContext, cancellationToken);
        view = AdminModerationQueueEndpoints.Merge(view, savedView?.View);
        status = AdminModerationQueueEndpoints.Merge(status, savedView?.Status);
        category = AdminModerationQueueEndpoints.Merge(category, savedView?.Category);
        search = AdminModerationQueueEndpoints.Merge(search, savedView?.Search);
        assigned = AdminModerationQueueEndpoints.Merge(assigned, savedView?.Assigned);
        priority = AdminModerationQueueEndpoints.Merge(priority, savedView?.Priority);
        sla = AdminModerationQueueEndpoints.Merge(sla, savedView?.Sla);
        sort = AdminModerationQueueEndpoints.Merge(sort, savedView?.Sort);

        var result = await BuildFilteredSupportQueueAsync(
            view,
            status,
            category,
            search,
            assigned,
            priority,
            sla,
            sort,
            principal,
            dbContext,
            timeProvider,
            configuration,
            cancellationToken);

        return result.Validation is not null
            ? result.Validation
            : HttpResults.Text(BuildSupportQueueCsv(result.Items), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> GetSupportQualityReportAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        string? category,
        string? priority,
        Guid? assignedSupportUserId,
        string? createdByRole,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var result = await BuildSupportQualityReportAsync(
            fromUtc,
            toUtc,
            bucket,
            category,
            priority,
            assignedSupportUserId,
            createdByRole,
            dbContext,
            timeProvider,
            configuration,
            cancellationToken);

        return result.Validation is not null
            ? result.Validation
            : HttpResults.Ok(result.Report);
    }

    private static async Task<IResult> ExportSupportQualityReportCsvAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        string? category,
        string? priority,
        Guid? assignedSupportUserId,
        string? createdByRole,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var result = await BuildSupportQualityReportAsync(
            fromUtc,
            toUtc,
            bucket,
            category,
            priority,
            assignedSupportUserId,
            createdByRole,
            dbContext,
            timeProvider,
            configuration,
            cancellationToken);

        return result.Validation is not null
            ? result.Validation
            : HttpResults.Text(BuildSupportQualityReportCsv(result.Report!), "text/csv", Encoding.UTF8);
    }

    private static async Task<IResult> GetSupportSavedViewsAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var supportUserId))
        {
            return UserNotFound();
        }

        var views = await dbContext.AdminQueueSavedViews
            .AsNoTracking()
            .Where(item => item.AdminUserId == supportUserId && item.Queue == SupportSavedViewQueue)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return HttpResults.Ok(views.Select(MapSavedView).ToArray());
    }

    private static async Task<IResult> CreateSupportSavedViewAsync(
        AdminQueueSavedViewRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var supportUserId))
        {
            return UserNotFound();
        }

        if (!TryBuildSupportSavedViewFilters(request, out var filters, out var validation))
        {
            return validation!;
        }

        AdminQueueSavedView view;
        try
        {
            view = new AdminQueueSavedView(supportUserId, SupportSavedViewQueue, request.Name ?? string.Empty, filters, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return Validation("name", exception.Message);
        }

        if (request.IsDefault == true)
        {
            await ClearDefaultSupportViewsAsync(supportUserId, dbContext, timeProvider.GetUtcNow(), cancellationToken);
            view.MarkDefault(timeProvider.GetUtcNow());
        }

        dbContext.AdminQueueSavedViews.Add(view);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Created($"/api/support/tickets/views/{view.Id}", MapSavedView(view));
    }

    private static async Task<IResult> UpdateSupportSavedViewAsync(
        Guid viewId,
        AdminQueueSavedViewRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var supportUserId))
        {
            return UserNotFound();
        }

        var view = await dbContext.AdminQueueSavedViews
            .SingleOrDefaultAsync(item => item.Id == viewId && item.AdminUserId == supportUserId && item.Queue == SupportSavedViewQueue, cancellationToken);
        if (view is null)
        {
            return SavedViewNotFound();
        }

        if (!TryBuildSupportSavedViewFilters(request, out var filters, out var validation))
        {
            return validation!;
        }

        try
        {
            view.RenameAndUpdate(request.Name ?? string.Empty, filters, timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            return Validation("name", exception.Message);
        }

        if (request.IsDefault == true)
        {
            await ClearDefaultSupportViewsAsync(supportUserId, dbContext, timeProvider.GetUtcNow(), cancellationToken);
            view.MarkDefault(timeProvider.GetUtcNow());
        }
        else if (request.IsDefault == false)
        {
            view.ClearDefault(timeProvider.GetUtcNow());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(MapSavedView(view));
    }

    private static async Task<IResult> DeleteSupportSavedViewAsync(
        Guid viewId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var supportUserId))
        {
            return UserNotFound();
        }

        var view = await dbContext.AdminQueueSavedViews
            .SingleOrDefaultAsync(item => item.Id == viewId && item.AdminUserId == supportUserId && item.Queue == SupportSavedViewQueue, cancellationToken);
        if (view is null)
        {
            return SavedViewNotFound();
        }

        dbContext.AdminQueueSavedViews.Remove(view);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.NoContent();
    }

    private static async Task<IResult> MakeDefaultSupportSavedViewAsync(
        Guid viewId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var supportUserId))
        {
            return UserNotFound();
        }

        var view = await dbContext.AdminQueueSavedViews
            .SingleOrDefaultAsync(item => item.Id == viewId && item.AdminUserId == supportUserId && item.Queue == SupportSavedViewQueue, cancellationToken);
        if (view is null)
        {
            return SavedViewNotFound();
        }

        await ClearDefaultSupportViewsAsync(supportUserId, dbContext, timeProvider.GetUtcNow(), cancellationToken);
        view.MarkDefault(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(MapSavedView(view));
    }

    private static async Task<IResult> AddBuyerMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        return await AddCustomerMessageAsync(ticketId, request, principal, SwyftlyRoles.Buyer, buyerId: buyer.Id, sellerId: null, dbContext, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddSellerMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var seller = await GetCurrentSellerAsync(principal, dbContext, cancellationToken);
        if (seller is null)
        {
            return SellerNotFound();
        }

        return await AddCustomerMessageAsync(ticketId, request, principal, SwyftlyRoles.Seller, buyerId: null, sellerId: seller.Id, dbContext, timeProvider, cancellationToken);
    }

    private static async Task<IResult> AddCustomerMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        string role,
        Guid? buyerId,
        Guid? sellerId,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(
                ticket => ticket.Id == ticketId
                    && (buyerId == null || ticket.BuyerId == buyerId)
                    && (sellerId == null || ticket.SellerId == sellerId),
                cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        try
        {
            ticket.AddCustomerMessage(userId, role, request.Message, timeProvider.GetUtcNow());
            dbContext.SupportMessages.Add(ticket.Messages.OrderBy(message => message.CreatedAtUtc).Last());
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: false));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.ParamName ?? "message", exception.Message);
        }
    }

    private static async Task<IResult> AddSupportMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        INotificationService notificationService,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        return await AddSupportTicketMessageAsync(
            ticketId,
            request,
            principal,
            isInternal: false,
            dbContext,
            notificationService,
            timeProvider,
            loggerFactory,
            cancellationToken);
    }

    private static async Task<IResult> AddSupportInternalNoteAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        return await AddSupportTicketMessageAsync(
            ticketId,
            request,
            principal,
            isInternal: true,
            dbContext,
            notificationService: null,
            timeProvider,
            loggerFactory: null,
            cancellationToken);
    }

    private static async Task<IResult> AddSupportTicketMessageAsync(
        Guid ticketId,
        SupportMessageRequest request,
        ClaimsPrincipal principal,
        bool isInternal,
        SwyftlyDbContext dbContext,
        INotificationService? notificationService,
        TimeProvider timeProvider,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        try
        {
            if (isInternal)
            {
                ticket.AddInternalNote(userId, GetSupportActorRole(principal), request.Message, timeProvider.GetUtcNow());
            }
            else
            {
                ticket.AddSupportResponse(userId, GetSupportActorRole(principal), request.Message, timeProvider.GetUtcNow());
            }

            dbContext.SupportMessages.Add(ticket.Messages.OrderBy(message => message.CreatedAtUtc).Last());
            await dbContext.SaveChangesAsync(cancellationToken);
            if (!isInternal && notificationService is not null && loggerFactory is not null && ticket.BuyerId.HasValue)
            {
                await BuyerNotificationDispatcher.NotifyBuyerAsync(
                    ticket.BuyerId.Value,
                    "SupportReply",
                    "Support replied to your ticket",
                    "A support agent replied to your support ticket.",
                    "SupportTicket",
                    ticket.Id,
                    timeProvider.GetUtcNow(),
                    dbContext,
                    notificationService,
                    loggerFactory.CreateLogger(nameof(SupportTicketEndpoints)),
                    cancellationToken);
            }

            return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.ParamName ?? "message", exception.Message);
        }
    }

    private static async Task<IResult> ClaimTicketAsync(
        Guid ticketId,
        bool? force,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        var previous = SupportTicketAuditSnapshot.From(ticket);
        try
        {
            ticket.Claim(userId, CanOverrideAssignment(principal) && force == true, timeProvider.GetUtcNow());
            await AddSupportAuditLogAsync(auditLogService, principal, httpContext, "SupportTicketClaimed", ticket, previous, null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.ParamName ?? "ticket", exception.Message);
        }
    }

    private static async Task<IResult> UnclaimTicketAsync(
        Guid ticketId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        var previous = SupportTicketAuditSnapshot.From(ticket);
        try
        {
            ticket.Unclaim(userId, CanOverrideAssignment(principal), timeProvider.GetUtcNow());
            await AddSupportAuditLogAsync(auditLogService, principal, httpContext, "SupportTicketUnclaimed", ticket, previous, null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private static async Task<IResult> TriageTicketAsync(
        Guid ticketId,
        SupportTicketTriageRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        if (!Enum.TryParse<SupportTicketPriority>(request.Priority, ignoreCase: true, out var priority))
        {
            return Validation("priority", $"Priority must be one of: {string.Join(", ", Enum.GetNames<SupportTicketPriority>())}.");
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        var previous = SupportTicketAuditSnapshot.From(ticket);
        var now = timeProvider.GetUtcNow();
        ticket.SetPriority(priority, now);
        if (!string.IsNullOrWhiteSpace(request.InternalNote))
        {
            try
            {
                ticket.AddInternalNote(userId, GetSupportActorRole(principal), request.InternalNote, now);
                dbContext.SupportMessages.Add(ticket.Messages.OrderBy(message => message.CreatedAtUtc).Last());
            }
            catch (ArgumentException exception)
            {
                return Validation(exception.ParamName ?? "internalNote", exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(exception.Message);
            }
        }

        await AddSupportAuditLogAsync(auditLogService, principal, httpContext, "SupportTicketTriaged", ticket, previous, request.InternalNote, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
    }

    private static async Task<IResult> EscalateTicketAsync(
        Guid ticketId,
        SupportTicketEscalationRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        SwyftlyDbContext dbContext,
        IAuditLogService auditLogService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return UserNotFound();
        }

        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        var previous = SupportTicketAuditSnapshot.From(ticket);
        try
        {
            ticket.Escalate(userId, request.Reason, timeProvider.GetUtcNow());
            await AddSupportAuditLogAsync(auditLogService, principal, httpContext, "SupportTicketEscalated", ticket, previous, request.Reason, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return Validation(exception.ParamName ?? "reason", exception.Message);
        }
    }

    private static async Task<IResult> ResolveTicketAsync(
        Guid ticketId,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        try
        {
            ticket.Resolve(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private static async Task<IResult> CloseTicketAsync(
        Guid ticketId,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var ticket = await TicketQueryForUpdate(dbContext)
            .SingleOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketNotFound();
        }

        ticket.Close(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return HttpResults.Ok(Map(ticket, includeInternalMessages: true));
    }

    private static async Task<IResult?> ValidateBuyerLinksAsync(
        CreateSupportTicketRequest request,
        Guid buyerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.LinkedOrderId.HasValue)
        {
            var orderExists = await dbContext.Orders
                .AnyAsync(order => order.Id == request.LinkedOrderId && order.BuyerId == buyerId, cancellationToken);
            if (!orderExists)
            {
                return LinkedRecordNotFound("Order");
            }
        }

        if (request.LinkedPaymentId.HasValue)
        {
            var paymentExists = await dbContext.Payments
                .AnyAsync(payment => payment.Id == request.LinkedPaymentId && payment.BuyerId == buyerId, cancellationToken);
            if (!paymentExists)
            {
                return LinkedRecordNotFound("Payment");
            }
        }

        return await ValidateSharedLinksAsync(request, dbContext, cancellationToken);
    }

    private static async Task<IResult?> ValidateSellerLinksAsync(
        CreateSupportTicketRequest request,
        Guid sellerId,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.LinkedOrderId.HasValue)
        {
            var orderExists = await dbContext.Orders
                .AnyAsync(order => order.Id == request.LinkedOrderId && order.SellerId == sellerId, cancellationToken);
            if (!orderExists)
            {
                return LinkedRecordNotFound("Order");
            }
        }

        if (request.LinkedProductId.HasValue)
        {
            var productExists = await dbContext.Products
                .AnyAsync(product => product.Id == request.LinkedProductId && product.SellerId == sellerId, cancellationToken);
            if (!productExists)
            {
                return LinkedRecordNotFound("Product");
            }
        }

        if (request.LinkedSellerId.HasValue && request.LinkedSellerId != sellerId)
        {
            return LinkedRecordNotFound("Seller");
        }

        if (request.LinkedPaymentId.HasValue)
        {
            var paymentExists = await dbContext.Payments
                .Join(
                    dbContext.Orders,
                    payment => payment.OrderId,
                    order => order.Id,
                    (payment, order) => new { payment, order })
                .AnyAsync(item => item.payment.Id == request.LinkedPaymentId && item.order.SellerId == sellerId, cancellationToken);
            if (!paymentExists)
            {
                return LinkedRecordNotFound("Payment");
            }
        }

        return await ValidateSharedLinksAsync(request, dbContext, cancellationToken);
    }

    private static async Task<IResult?> ValidateSharedLinksAsync(
        CreateSupportTicketRequest request,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.LinkedProductId.HasValue)
        {
            var productExists = await dbContext.Products.AnyAsync(product => product.Id == request.LinkedProductId, cancellationToken);
            if (!productExists)
            {
                return LinkedRecordNotFound("Product");
            }
        }

        if (request.LinkedSellerId.HasValue)
        {
            var sellerExists = await dbContext.SellerProfiles.AnyAsync(seller => seller.Id == request.LinkedSellerId, cancellationToken);
            if (!sellerExists)
            {
                return LinkedRecordNotFound("Seller");
            }
        }

        return null;
    }

    private static async Task<SupportQueueBuildResult> BuildFilteredSupportQueueAsync(
        string? view,
        string? status,
        string? category,
        string? search,
        string? assigned,
        string? priority,
        string? sla,
        string? sort,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!TryParseOptionalStatus(status, out var parsedStatus))
        {
            return new SupportQueueBuildResult([], Validation("status", $"Status must be one of: {string.Join(", ", Enum.GetNames<SupportTicketStatus>())}."));
        }

        if (!TryParseOptionalCategory(category, out var parsedCategory))
        {
            return new SupportQueueBuildResult([], InvalidCategory());
        }

        if (!TryParseOptionalPriority(priority, out var parsedPriority))
        {
            return new SupportQueueBuildResult([], Validation("priority", $"Priority must be one of: {string.Join(", ", Enum.GetNames<SupportTicketPriority>())}."));
        }

        if (!IsKnownSla(sla))
        {
            return new SupportQueueBuildResult([], Validation("sla", "SLA must be one of: OnTrack, DueSoon, Overdue."));
        }

        var normalizedView = string.IsNullOrWhiteSpace(view) ? "NeedsAttention" : view.Trim();
        if (!new[] { "NeedsAttention", "All" }.Contains(normalizedView, StringComparer.OrdinalIgnoreCase))
        {
            return new SupportQueueBuildResult([], Validation("view", "View must be NeedsAttention or All."));
        }

        var now = timeProvider.GetUtcNow();
        var options = ReadSupportOperationsOptions(configuration);
        var tickets = await TicketQuery(dbContext).ToListAsync(cancellationToken);
        var assignedNames = await ReadAssignedUserNamesAsync(tickets, dbContext, cancellationToken);
        var actorUserId = TryGetUserId(principal, out var parsedActorUserId) ? parsedActorUserId : (Guid?)null;
        var normalizedSearch = search?.Trim().ToLowerInvariant();

        var items = tickets
            .Select(ticket => MapQueueItem(ticket, assignedNames, now, options))
            .Where(item => string.Equals(normalizedView, "All", StringComparison.OrdinalIgnoreCase)
                || item.Status is not (nameof(SupportTicketStatus.Resolved) or nameof(SupportTicketStatus.Closed)))
            .Where(item => parsedStatus is null || string.Equals(item.Status, parsedStatus.Value.ToString(), StringComparison.Ordinal))
            .Where(item => parsedCategory is null || string.Equals(item.Category, parsedCategory.Value.ToString(), StringComparison.Ordinal))
            .Where(item => parsedPriority is null || string.Equals(item.Priority, parsedPriority.Value.ToString(), StringComparison.Ordinal))
            .Where(item => MatchesAssignment(item, assigned, actorUserId))
            .Where(item => string.IsNullOrWhiteSpace(sla) || string.Equals(item.SlaStatus, sla.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(normalizedSearch)
                || string.Join(' ', item.SupportTicketId, item.Subject, item.Description, item.CreatedByRole, item.BuyerId, item.SellerId, item.LinkedOrderId, item.LinkedPaymentId, item.LatestInternalNote)
                    .ToLowerInvariant()
                    .Contains(normalizedSearch))
            .ToArray();

        return new SupportQueueBuildResult(SortQueueItems(items, sort).ToArray(), null);
    }

    private static SupportTicketQueueItemResponse MapQueueItem(
        SupportTicket ticket,
        IReadOnlyDictionary<Guid, string> assignedUserNames,
        DateTimeOffset now,
        SupportOperationsOptions options)
    {
        var latestInternalNote = ticket.Messages
            .Where(message => message.IsInternal)
            .OrderByDescending(message => message.CreatedAtUtc)
            .Select(message => message.Message)
            .FirstOrDefault();
        var sla = CalculateSupportSla(ticket, now, options);
        return new SupportTicketQueueItemResponse(
            ticket.Id,
            ticket.CreatedByUserId,
            ticket.CreatedByRole,
            ticket.BuyerId,
            ticket.SellerId,
            ticket.Category.ToString(),
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.Subject,
            ticket.Description,
            ticket.LinkedOrderId,
            ticket.LinkedProductId,
            ticket.LinkedSellerId,
            ticket.LinkedPaymentId,
            ticket.AssignedSupportUserId,
            ticket.AssignedSupportUserId.HasValue && assignedUserNames.TryGetValue(ticket.AssignedSupportUserId.Value, out var assignedName) ? assignedName : null,
            ticket.OpenedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.ResolvedAtUtc,
            ticket.ClosedAtUtc,
            ticket.EscalationReason,
            ticket.EscalatedAtUtc,
            ticket.EscalatedByUserId,
            latestInternalNote,
            ticket.Messages.Count,
            sla.AgeHours,
            sla.SlaStatus,
            sla.SlaDueAtUtc);
    }

    private static SupportSlaResponse CalculateSupportSla(SupportTicket ticket, DateTimeOffset now, SupportOperationsOptions options)
    {
        var thresholdHours = ticket.Status == SupportTicketStatus.Escalated
            ? options.EscalatedSlaHours
            : options.OpenSlaHours;
        var dueSoonHours = Math.Min(options.DueSoonHours, Math.Max(1, thresholdHours));
        var startedAtUtc = ticket.Status == SupportTicketStatus.Escalated && ticket.EscalatedAtUtc.HasValue
            ? ticket.EscalatedAtUtc.Value
            : ticket.OpenedAtUtc;
        var dueAtUtc = startedAtUtc.AddHours(thresholdHours);
        var ageHours = Math.Max(0, (int)Math.Floor((now - startedAtUtc).TotalHours));
        var status = ticket.Status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed
            ? SupportSlaStatus.OnTrack
            : now >= dueAtUtc
                ? SupportSlaStatus.Overdue
                : now >= dueAtUtc.AddHours(-dueSoonHours)
                    ? SupportSlaStatus.DueSoon
                    : SupportSlaStatus.OnTrack;

        return new SupportSlaResponse(ageHours, status, dueAtUtc);
    }

    private static SupportOperationsOptions ReadSupportOperationsOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(SupportOperationsOptions.SectionName).Get<SupportOperationsOptions>() ?? new SupportOperationsOptions();
        return new SupportOperationsOptions
        {
            OpenSlaHours = Math.Max(1, options.OpenSlaHours),
            EscalatedSlaHours = Math.Max(1, options.EscalatedSlaHours),
            DueSoonHours = Math.Max(1, options.DueSoonHours)
        };
    }

    private static SupportQualityOptions ReadSupportQualityOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(SupportQualityOptions.SectionName).Get<SupportQualityOptions>() ?? new SupportQualityOptions();
        return new SupportQualityOptions
        {
            DefaultRangeDays = Math.Clamp(options.DefaultRangeDays, 1, 90),
            MaxRangeDays = Math.Clamp(options.MaxRangeDays, 1, 366),
            FirstResponseTargetHours = Math.Max(1, options.FirstResponseTargetHours),
            ResolutionTargetHours = Math.Max(1, options.ResolutionTargetHours)
        };
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ReadAssignedUserNamesAsync(
        IReadOnlyCollection<SupportTicket> tickets,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIds = tickets
            .Select(ticket => ticket.AssignedSupportUserId)
            .Where(userId => userId.HasValue)
            .Select(userId => userId!.Value)
            .Distinct()
            .ToArray();

        return userIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.UserName ?? user.Email ?? user.Id.ToString(), cancellationToken);
    }

    private static bool MatchesAssignment(SupportTicketQueueItemResponse item, string? assigned, Guid? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(assigned) || string.Equals(assigned, "Any", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(assigned, "Unassigned", StringComparison.OrdinalIgnoreCase))
        {
            return item.AssignedSupportUserId is null;
        }

        return string.Equals(assigned, "Mine", StringComparison.OrdinalIgnoreCase)
            && actorUserId.HasValue
            && item.AssignedSupportUserId == actorUserId.Value;
    }

    private static IEnumerable<SupportTicketQueueItemResponse> SortQueueItems(
        IEnumerable<SupportTicketQueueItemResponse> items,
        string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "UpdatedDesc" : sort.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "openedasc" => items.OrderBy(item => item.OpenedAtUtc),
            "openeddesc" => items.OrderByDescending(item => item.OpenedAtUtc),
            "sladueasc" => items.OrderBy(item => item.SlaDueAtUtc),
            "prioritydesc" => items.OrderByDescending(item => PriorityRank(item.Priority)).ThenBy(item => item.SlaDueAtUtc),
            _ => items.OrderByDescending(item => item.UpdatedAtUtc)
        };
    }

    private static int PriorityRank(string priority) =>
        priority switch
        {
            nameof(SupportTicketPriority.Urgent) => 3,
            nameof(SupportTicketPriority.High) => 2,
            _ => 1
        };

    private static IReadOnlyCollection<SupportTicketCountResponse> CountBy<T>(
        IEnumerable<SupportTicketQueueItemResponse> items,
        Func<SupportTicketQueueItemResponse, T> keySelector) where T : notnull =>
        items.GroupBy(keySelector)
            .Select(group => new SupportTicketCountResponse(group.Key.ToString() ?? string.Empty, group.Count()))
            .OrderBy(item => item.Key)
            .ToArray();

    private static async Task<AdminQueueSavedView?> GetSupportSavedViewForRequestAsync(
        Guid? savedViewId,
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!savedViewId.HasValue || !TryGetUserId(principal, out var supportUserId))
        {
            return null;
        }

        return await dbContext.AdminQueueSavedViews
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == savedViewId.Value && item.AdminUserId == supportUserId && item.Queue == SupportSavedViewQueue, cancellationToken);
    }

    private static bool TryBuildSupportSavedViewFilters(
        AdminQueueSavedViewRequest request,
        out AdminQueueSavedViewFilters filters,
        out IResult? validation)
    {
        filters = new AdminQueueSavedViewFilters(null, null, null, null, null, null, null, null, null, null, null);
        validation = null;

        if (!string.IsNullOrWhiteSpace(request.Queue) && !string.Equals(request.Queue.Trim(), SupportSavedViewQueue, StringComparison.OrdinalIgnoreCase))
        {
            validation = Validation("queue", "Support ticket saved views must use the Support queue.");
            return false;
        }

        if (!TryParseOptionalStatus(request.Filters?.Status, out _))
        {
            validation = Validation("status", $"Status must be one of: {string.Join(", ", Enum.GetNames<SupportTicketStatus>())}.");
            return false;
        }

        if (!TryParseOptionalCategory(request.Filters?.Category, out _))
        {
            validation = InvalidCategory();
            return false;
        }

        if (!TryParseOptionalPriority(request.Filters?.Priority, out _))
        {
            validation = Validation("priority", $"Priority must be one of: {string.Join(", ", Enum.GetNames<SupportTicketPriority>())}.");
            return false;
        }

        if (!IsKnownSla(request.Filters?.Sla))
        {
            validation = Validation("sla", "SLA must be one of: OnTrack, DueSoon, Overdue.");
            return false;
        }

        filters = new AdminQueueSavedViewFilters(
            request.Filters?.View,
            request.Filters?.Status,
            request.Filters?.Category,
            request.Filters?.Search,
            null,
            request.Filters?.Assigned,
            request.Filters?.Priority,
            null,
            request.Filters?.Sla,
            request.Filters?.Sort,
            request.Filters?.PageSize);
        return true;
    }

    private static AdminQueueSavedViewResponse MapSavedView(AdminQueueSavedView view) =>
        new(
            view.Id,
            view.Queue,
            view.Name,
            view.IsDefault,
            new AdminQueueSavedViewFiltersResponse(
                view.View,
                view.Status,
                view.Category,
                view.Search,
                view.SellerId,
                view.Assigned,
                view.Priority,
                view.HasNotes,
                view.Sla,
                view.Sort,
                view.PageSize),
            view.CreatedAtUtc,
            view.UpdatedAtUtc);

    private static async Task ClearDefaultSupportViewsAsync(
        Guid supportUserId,
        SwyftlyDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingDefaults = await dbContext.AdminQueueSavedViews
            .Where(item => item.AdminUserId == supportUserId && item.Queue == SupportSavedViewQueue && item.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var view in existingDefaults)
        {
            view.ClearDefault(now);
        }
    }

    private static string BuildSupportQueueCsv(IReadOnlyCollection<SupportTicketQueueItemResponse> rows)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "supportTicketId", "createdByRole", "category", "status", "priority", "slaStatus", "assignedSupportUserId", "assignedSupportDisplayName", "subject", "openedAtUtc", "updatedAtUtc", "ageHours", "linkedOrderId", "linkedPaymentId", "linkedProductId", "linkedSellerId", "latestInternalNote");
        foreach (var row in rows)
        {
            AppendCsvLine(
                builder,
                row.SupportTicketId.ToString(),
                row.CreatedByRole,
                row.Category,
                row.Status,
                row.Priority,
                row.SlaStatus,
                row.AssignedSupportUserId?.ToString() ?? string.Empty,
                row.AssignedSupportDisplayName ?? string.Empty,
                row.Subject,
                row.OpenedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                row.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                row.AgeHours.ToString(CultureInfo.InvariantCulture),
                row.LinkedOrderId?.ToString() ?? string.Empty,
                row.LinkedPaymentId?.ToString() ?? string.Empty,
                row.LinkedProductId?.ToString() ?? string.Empty,
                row.LinkedSellerId?.ToString() ?? string.Empty,
                row.LatestInternalNote ?? string.Empty);
        }

        return builder.ToString();
    }

    private static async Task<SupportQualityReportBuildResult> BuildSupportQualityReportAsync(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? bucket,
        string? category,
        string? priority,
        Guid? assignedSupportUserId,
        string? createdByRole,
        SwyftlyDbContext dbContext,
        TimeProvider timeProvider,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var options = ReadSupportQualityOptions(configuration);
        var to = toUtc ?? now;
        var from = fromUtc ?? to.AddDays(-options.DefaultRangeDays);
        if (from > to)
        {
            return new SupportQualityReportBuildResult(null, Validation("fromUtc", "fromUtc must be before or equal to toUtc."));
        }

        if ((to - from).TotalDays > options.MaxRangeDays)
        {
            return new SupportQualityReportBuildResult(null, Validation("toUtc", $"Date range cannot exceed {options.MaxRangeDays} days."));
        }

        var normalizedBucket = string.IsNullOrWhiteSpace(bucket) ? "Day" : bucket.Trim();
        if (!new[] { "Day", "Week" }.Contains(normalizedBucket, StringComparer.OrdinalIgnoreCase))
        {
            return new SupportQualityReportBuildResult(null, Validation("bucket", "Bucket must be Day or Week."));
        }

        if (!TryParseOptionalCategory(category, out var parsedCategory))
        {
            return new SupportQualityReportBuildResult(null, InvalidCategory());
        }

        if (!TryParseOptionalPriority(priority, out var parsedPriority))
        {
            return new SupportQualityReportBuildResult(null, Validation("priority", $"Priority must be one of: {string.Join(", ", Enum.GetNames<SupportTicketPriority>())}."));
        }

        if (!IsKnownRequesterRole(createdByRole))
        {
            return new SupportQualityReportBuildResult(null, Validation("createdByRole", "createdByRole must be Buyer, Seller, SupportAgent, Admin, or SuperAdmin."));
        }

        var supportOptions = ReadSupportOperationsOptions(configuration);
        var tickets = await TicketQuery(dbContext).ToListAsync(cancellationToken);
        var assignedNames = await ReadAssignedUserNamesAsync(tickets, dbContext, cancellationToken);
        var filtered = tickets
            .Where(ticket => parsedCategory is null || ticket.Category == parsedCategory.Value)
            .Where(ticket => parsedPriority is null || ticket.Priority == parsedPriority.Value)
            .Where(ticket => !assignedSupportUserId.HasValue || ticket.AssignedSupportUserId == assignedSupportUserId.Value)
            .Where(ticket => string.IsNullOrWhiteSpace(createdByRole) || string.Equals(ticket.CreatedByRole, createdByRole.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var qualityRows = filtered
            .Select(ticket => BuildSupportQualityTicket(ticket, now, options, supportOptions, assignedNames))
            .ToArray();
        var createdRows = qualityRows
            .Where(row => IsWithinRange(row.OpenedAtUtc, from, to))
            .ToArray();
        var resolvedRows = qualityRows
            .Where(row => IsWithinRange(row.ResolvedAtUtc, from, to))
            .ToArray();

        var summary = new SupportQualitySummaryResponse(
            createdRows.Length,
            resolvedRows.Length,
            qualityRows.Count(row => IsWithinRange(row.ClosedAtUtc, from, to)),
            qualityRows.Count(row => IsWithinRange(row.EscalatedAtUtc, from, to)),
            qualityRows.Count(row => row.Status is not (nameof(SupportTicketStatus.Resolved) or nameof(SupportTicketStatus.Closed))),
            qualityRows.Count(row => row.SlaStatus == SupportSlaStatus.Overdue),
            Average(createdRows.Select(row => row.FirstResponseHours)),
            Average(resolvedRows.Select(row => row.ResolutionHours)),
            createdRows.Count(row => row.FirstResponseTargetMet == true),
            createdRows.Count(row => row.FirstResponseTargetMet == false),
            resolvedRows.Count(row => row.ResolutionTargetMet == true),
            resolvedRows.Count(row => row.ResolutionTargetMet == false));

        var report = new SupportTicketQualityReportResponse(
            now,
            from,
            to,
            normalizedBucket.Equals("Week", StringComparison.OrdinalIgnoreCase) ? "Week" : "Day",
            summary,
            BuildQualityTrend(qualityRows, from, to, normalizedBucket),
            BuildQualityBreakdown(createdRows, row => row.Category),
            BuildQualityBreakdown(createdRows, row => row.Priority),
            BuildQualityBreakdown(createdRows, row => row.CreatedByRole),
            BuildQualityBreakdown(qualityRows, row => row.SlaStatus),
            BuildAssigneeQualityBreakdown(createdRows));

        return new SupportQualityReportBuildResult(report, null);
    }

    private static SupportQualityTicket BuildSupportQualityTicket(
        SupportTicket ticket,
        DateTimeOffset now,
        SupportQualityOptions options,
        SupportOperationsOptions supportOptions,
        IReadOnlyDictionary<Guid, string> assignedNames)
    {
        var firstResponse = ticket.Messages
            .Where(message => !message.IsInternal && IsSupportActorRole(message.SenderRole) && message.CreatedAtUtc >= ticket.OpenedAtUtc)
            .OrderBy(message => message.CreatedAtUtc)
            .FirstOrDefault();
        var firstResponseHours = firstResponse is null ? (double?)null : (firstResponse.CreatedAtUtc - ticket.OpenedAtUtc).TotalHours;
        var resolutionHours = ticket.ResolvedAtUtc.HasValue && ticket.ResolvedAtUtc.Value >= ticket.OpenedAtUtc
            ? (ticket.ResolvedAtUtc.Value - ticket.OpenedAtUtc).TotalHours
            : (double?)null;
        var firstResponseTargetMet = CalculateTargetMet(firstResponseHours, ticket.OpenedAtUtc, now, options.FirstResponseTargetHours);
        var resolutionTargetMet = CalculateTargetMet(resolutionHours, ticket.OpenedAtUtc, now, options.ResolutionTargetHours);
        var sla = CalculateSupportSla(ticket, now, supportOptions);
        var assigneeName = ticket.AssignedSupportUserId.HasValue && assignedNames.TryGetValue(ticket.AssignedSupportUserId.Value, out var name)
            ? name
            : null;

        return new SupportQualityTicket(
            ticket.Id,
            ticket.Category.ToString(),
            ticket.Priority.ToString(),
            ticket.CreatedByRole,
            ticket.Status.ToString(),
            ticket.AssignedSupportUserId,
            assigneeName,
            ticket.OpenedAtUtc,
            ticket.ResolvedAtUtc,
            ticket.ClosedAtUtc,
            ticket.EscalatedAtUtc,
            firstResponseHours.HasValue ? Math.Round(firstResponseHours.Value, 1) : null,
            resolutionHours.HasValue ? Math.Round(resolutionHours.Value, 1) : null,
            firstResponseTargetMet,
            resolutionTargetMet,
            sla.SlaStatus);
    }

    private static bool? CalculateTargetMet(double? completedHours, DateTimeOffset openedAtUtc, DateTimeOffset now, int targetHours)
    {
        if (completedHours.HasValue)
        {
            return completedHours.Value <= targetHours;
        }

        return (now - openedAtUtc).TotalHours >= targetHours ? false : null;
    }

    private static bool IsWithinRange(DateTimeOffset? value, DateTimeOffset from, DateTimeOffset to) =>
        value.HasValue && value.Value >= from && value.Value <= to;

    private static IReadOnlyCollection<SupportQualityTrendBucketResponse> BuildQualityTrend(
        IReadOnlyCollection<SupportQualityTicket> rows,
        DateTimeOffset from,
        DateTimeOffset to,
        string bucket)
    {
        var starts = new List<DateTimeOffset>();
        var cursor = StartOfBucket(from, bucket);
        while (cursor <= to)
        {
            starts.Add(cursor);
            cursor = AddBucket(cursor, bucket);
        }

        return starts.Select(start =>
            {
                var end = AddBucket(start, bucket);
                var createdRows = rows.Where(row => row.OpenedAtUtc >= start && row.OpenedAtUtc < end).ToArray();
                var resolvedRows = rows.Where(row => row.ResolvedAtUtc >= start && row.ResolvedAtUtc < end).ToArray();
                return new SupportQualityTrendBucketResponse(
                    start,
                    end,
                    createdRows.Length,
                    resolvedRows.Length,
                    rows.Count(row => row.EscalatedAtUtc >= start && row.EscalatedAtUtc < end),
                    Average(createdRows.Select(row => row.FirstResponseHours)),
                    Average(resolvedRows.Select(row => row.ResolutionHours)));
            })
            .ToArray();
    }

    private static DateTimeOffset StartOfBucket(DateTimeOffset value, string bucket)
    {
        var dayStart = new DateTimeOffset(value.Year, value.Month, value.Day, 0, 0, 0, TimeSpan.Zero);
        if (!bucket.Equals("Week", StringComparison.OrdinalIgnoreCase))
        {
            return dayStart;
        }

        var offset = ((int)dayStart.DayOfWeek + 6) % 7;
        return dayStart.AddDays(-offset);
    }

    private static DateTimeOffset AddBucket(DateTimeOffset value, string bucket) =>
        bucket.Equals("Week", StringComparison.OrdinalIgnoreCase) ? value.AddDays(7) : value.AddDays(1);

    private static IReadOnlyCollection<SupportQualityBreakdownResponse> BuildQualityBreakdown(
        IReadOnlyCollection<SupportQualityTicket> rows,
        Func<SupportQualityTicket, string> keySelector) =>
        rows.GroupBy(keySelector)
            .Select(group => new SupportQualityBreakdownResponse(
                group.Key,
                group.Count(),
                group.Count(row => row.ResolvedAtUtc.HasValue),
                group.Count(row => row.EscalatedAtUtc.HasValue),
                group.Count(row => row.FirstResponseTargetMet == false),
                group.Count(row => row.ResolutionTargetMet == false),
                Average(group.Select(row => row.FirstResponseHours)),
                Average(group.Select(row => row.ResolutionHours))))
            .OrderByDescending(item => item.CreatedCount)
            .ThenBy(item => item.Key)
            .ToArray();

    private static IReadOnlyCollection<SupportQualityAssigneeBreakdownResponse> BuildAssigneeQualityBreakdown(
        IReadOnlyCollection<SupportQualityTicket> rows) =>
        rows.GroupBy(row => row.AssignedSupportUserId?.ToString() ?? "Unassigned")
            .Select(group => new SupportQualityAssigneeBreakdownResponse(
                group.Key,
                group.First().AssignedSupportDisplayName,
                group.Count(),
                group.Count(row => row.ResolvedAtUtc.HasValue),
                group.Count(row => row.EscalatedAtUtc.HasValue),
                group.Count(row => row.FirstResponseTargetMet == false),
                group.Count(row => row.ResolutionTargetMet == false),
                Average(group.Select(row => row.FirstResponseHours)),
                Average(group.Select(row => row.ResolutionHours))))
            .OrderByDescending(item => item.CreatedCount)
            .ThenBy(item => item.AssignedSupportDisplayName ?? item.AssignedSupportUserId)
            .ToArray();

    private static double? Average(IEnumerable<double?> values)
    {
        var concrete = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return concrete.Length == 0 ? null : Math.Round(concrete.Average(), 1);
    }

    private static string BuildSupportQualityReportCsv(SupportTicketQualityReportResponse report)
    {
        var builder = new StringBuilder();
        AppendCsvLine(builder, "section", "key", "createdCount", "resolvedCount", "escalatedCount", "firstResponseTargetMissedCount", "resolutionTargetMissedCount", "averageFirstResponseHours", "averageResolutionHours");
        foreach (var row in report.CategoryBreakdown)
        {
            AppendQualityBreakdownCsv(builder, "category", row);
        }

        foreach (var row in report.PriorityBreakdown)
        {
            AppendQualityBreakdownCsv(builder, "priority", row);
        }

        foreach (var row in report.RequesterRoleBreakdown)
        {
            AppendQualityBreakdownCsv(builder, "requesterRole", row);
        }

        foreach (var row in report.SlaBreakdown)
        {
            AppendQualityBreakdownCsv(builder, "sla", row);
        }

        foreach (var row in report.AssigneeBreakdown)
        {
            AppendCsvLine(
                builder,
                "assignee",
                row.AssignedSupportDisplayName ?? row.AssignedSupportUserId,
                row.CreatedCount.ToString(CultureInfo.InvariantCulture),
                row.ResolvedCount.ToString(CultureInfo.InvariantCulture),
                row.EscalatedCount.ToString(CultureInfo.InvariantCulture),
                row.FirstResponseTargetMissedCount.ToString(CultureInfo.InvariantCulture),
                row.ResolutionTargetMissedCount.ToString(CultureInfo.InvariantCulture),
                row.AverageFirstResponseHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.AverageResolutionHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        AppendCsvLine(builder, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        AppendCsvLine(builder, "trendBucketStartUtc", "trendBucketEndUtc", "createdCount", "resolvedCount", "escalatedCount", "averageFirstResponseHours", "averageResolutionHours");
        foreach (var row in report.Trend)
        {
            AppendCsvLine(
                builder,
                row.BucketStartUtc.ToString("O", CultureInfo.InvariantCulture),
                row.BucketEndUtc.ToString("O", CultureInfo.InvariantCulture),
                row.CreatedCount.ToString(CultureInfo.InvariantCulture),
                row.ResolvedCount.ToString(CultureInfo.InvariantCulture),
                row.EscalatedCount.ToString(CultureInfo.InvariantCulture),
                row.AverageFirstResponseHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.AverageResolutionHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return builder.ToString();
    }

    private static void AppendQualityBreakdownCsv(StringBuilder builder, string section, SupportQualityBreakdownResponse row)
    {
        AppendCsvLine(
            builder,
            section,
            row.Key,
            row.CreatedCount.ToString(CultureInfo.InvariantCulture),
            row.ResolvedCount.ToString(CultureInfo.InvariantCulture),
            row.EscalatedCount.ToString(CultureInfo.InvariantCulture),
            row.FirstResponseTargetMissedCount.ToString(CultureInfo.InvariantCulture),
            row.ResolutionTargetMissedCount.ToString(CultureInfo.InvariantCulture),
            row.AverageFirstResponseHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            row.AverageResolutionHours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static void AppendCsvLine(StringBuilder builder, params string[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(Csv)));
    }

    private static string Csv(string value)
    {
        var sanitized = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return sanitized.Contains(',', StringComparison.Ordinal)
            || sanitized.Contains('"', StringComparison.Ordinal)
            || sanitized.Contains('\n', StringComparison.Ordinal)
            || sanitized.Contains('\r', StringComparison.Ordinal)
                ? $"\"{sanitized}\""
                : sanitized;
    }

    private static double? CalculateAverageFirstResponseHours(IReadOnlyCollection<SupportTicket> tickets)
    {
        var durations = tickets
            .Select(ticket =>
            {
                var firstSupportMessage = ticket.Messages
                    .Where(message => !message.IsInternal && IsSupportActorRole(message.SenderRole) && message.CreatedAtUtc >= ticket.OpenedAtUtc)
                    .OrderBy(message => message.CreatedAtUtc)
                    .FirstOrDefault();
                return firstSupportMessage is null ? (double?)null : (firstSupportMessage.CreatedAtUtc - ticket.OpenedAtUtc).TotalHours;
            })
            .Where(duration => duration.HasValue)
            .Select(duration => duration!.Value)
            .ToArray();

        return durations.Length == 0 ? null : Math.Round(durations.Average(), 1);
    }

    private static double? CalculateAverageResolutionHours(IReadOnlyCollection<SupportTicket> tickets)
    {
        var durations = tickets
            .Where(ticket => ticket.ResolvedAtUtc.HasValue && ticket.ResolvedAtUtc.Value >= ticket.OpenedAtUtc)
            .Select(ticket => (ticket.ResolvedAtUtc!.Value - ticket.OpenedAtUtc).TotalHours)
            .ToArray();

        return durations.Length == 0 ? null : Math.Round(durations.Average(), 1);
    }

    private static bool IsSupportActorRole(string role) =>
        string.Equals(role, SwyftlyRoles.SupportAgent, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, SwyftlyRoles.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, SwyftlyRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

    private static IQueryable<SupportTicket> TicketQuery(SwyftlyDbContext dbContext) =>
        dbContext.SupportTickets
            .Include(ticket => ticket.Messages)
            .AsNoTracking();

    private static IQueryable<SupportTicket> TicketQueryForUpdate(SwyftlyDbContext dbContext) =>
        dbContext.SupportTickets.Include(ticket => ticket.Messages);

    private static async Task<SupportTicketCustomerContextResponse?> BuildCustomerContextAsync(
        SupportTicket ticket,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var order = ticket.LinkedOrderId.HasValue
            ? await dbContext.Orders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == ticket.LinkedOrderId.Value, cancellationToken)
            : null;
        var payment = ticket.LinkedPaymentId.HasValue
            ? await dbContext.Payments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == ticket.LinkedPaymentId.Value, cancellationToken)
            : null;
        var product = ticket.LinkedProductId.HasValue
            ? await dbContext.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == ticket.LinkedProductId.Value, cancellationToken)
            : null;

        var buyerId = ticket.BuyerId ?? order?.BuyerId ?? payment?.BuyerId;
        var sellerId = ticket.SellerId ?? ticket.LinkedSellerId ?? order?.SellerId ?? product?.SellerId;
        var buyer = buyerId.HasValue
            ? await dbContext.BuyerProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == buyerId.Value, cancellationToken)
            : null;
        var seller = sellerId.HasValue
            ? await dbContext.SellerProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sellerId.Value, cancellationToken)
            : null;

        var userIds = new[] { buyer?.UserId, seller?.UserId }
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToArray();
        var users = userIds.Length == 0
            ? new Dictionary<Guid, string?>()
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => userIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.Email ?? user.UserName, cancellationToken);

        if (buyer is null && seller is null && order is null && payment is null && product is null)
        {
            return null;
        }

        return new SupportTicketCustomerContextResponse(
            buyer is null
                ? null
                : new SupportCustomerBuyerContextResponse(
                    buyer.Id,
                    buyer.UserId,
                    buyer.DisplayName,
                    users.TryGetValue(buyer.UserId, out var buyerEmail) ? buyerEmail : null,
                    buyer.PhoneNumber),
            seller is null
                ? null
                : new SupportCustomerSellerContextResponse(
                    seller.Id,
                    seller.UserId,
                    seller.DisplayName,
                    seller.ContactEmail ?? (users.TryGetValue(seller.UserId, out var sellerEmail) ? sellerEmail : null),
                    seller.PhoneNumber,
                    seller.VerificationStatus.ToString(),
                    $"/admin/sellers/{seller.Id}"),
            order is null
                ? null
                : new SupportCustomerOrderContextResponse(
                    order.Id,
                    order.Status.ToString(),
                    order.TotalAmount,
                    order.CreatedAtUtc,
                    order.BuyerId,
                    order.SellerId,
                    $"/admin/orders/{order.Id}"),
            payment is null
                ? null
                : new SupportCustomerPaymentContextResponse(
                    payment.Id,
                    payment.OrderId,
                    payment.Provider,
                    payment.Status.ToString(),
                    payment.Amount,
                    payment.Currency,
                    payment.PaidAtUtc,
                    payment.FailedAtUtc,
                    $"/admin/payments/{payment.Id}"),
            product is null
                ? null
                : new SupportCustomerProductContextResponse(
                    product.Id,
                    product.SellerId,
                    product.Title,
                    product.Slug,
                    product.Status.ToString(),
                    $"/admin/products/{product.Id}"));
    }

    private static SupportTicketResponse Map(
        SupportTicket ticket,
        bool includeInternalMessages,
        SupportTicketCustomerContextResponse? customerContext = null) =>
        new(
            ticket.Id,
            ticket.CreatedByUserId,
            ticket.CreatedByRole,
            ticket.BuyerId,
            ticket.SellerId,
            ticket.Category.ToString(),
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.Subject,
            ticket.Description,
            ticket.LinkedOrderId,
            ticket.LinkedProductId,
            ticket.LinkedSellerId,
            ticket.LinkedPaymentId,
            ticket.AssignedSupportUserId,
            ticket.EscalationReason,
            ticket.EscalatedAtUtc,
            ticket.EscalatedByUserId,
            ticket.OpenedAtUtc,
            ticket.ResolvedAtUtc,
            ticket.ClosedAtUtc,
            ticket.Messages
                .Where(message => includeInternalMessages || !message.IsInternal)
                .OrderBy(message => message.CreatedAtUtc)
                .Select(message => new SupportMessageResponse(
                    message.Id,
                    message.SenderUserId,
                    message.SenderRole,
                    message.Message,
                    message.IsInternal,
                    message.CreatedAtUtc))
                .ToArray(),
            customerContext);

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static async Task<SellerProfile?> GetCurrentSellerAsync(
        ClaimsPrincipal principal,
        SwyftlyDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.SellerProfiles.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken)
            : null;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }

    private static string GetSupportActorRole(ClaimsPrincipal principal)
    {
        if (principal.IsInRole(SwyftlyRoles.SuperAdmin))
        {
            return SwyftlyRoles.SuperAdmin;
        }

        return principal.IsInRole(SwyftlyRoles.Admin)
            ? SwyftlyRoles.Admin
            : SwyftlyRoles.SupportAgent;
    }

    private static bool CanOverrideAssignment(ClaimsPrincipal principal) =>
        principal.IsInRole(SwyftlyRoles.Admin) || principal.IsInRole(SwyftlyRoles.SuperAdmin);

    private static bool TryParseOptionalStatus(string? status, out SupportTicketStatus? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        if (Enum.TryParse<SupportTicketStatus>(status, ignoreCase: true, out var value))
        {
            parsed = value;
            return true;
        }

        return false;
    }

    private static bool TryParseOptionalCategory(string? category, out SupportTicketCategory? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(category))
        {
            return true;
        }

        if (Enum.TryParse<SupportTicketCategory>(category, ignoreCase: true, out var value))
        {
            parsed = value;
            return true;
        }

        return false;
    }

    private static bool TryParseOptionalPriority(string? priority, out SupportTicketPriority? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(priority))
        {
            return true;
        }

        if (Enum.TryParse<SupportTicketPriority>(priority, ignoreCase: true, out var value))
        {
            parsed = value;
            return true;
        }

        return false;
    }

    private static bool IsKnownSla(string? sla) =>
        string.IsNullOrWhiteSpace(sla)
        || string.Equals(sla, SupportSlaStatus.OnTrack, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sla, SupportSlaStatus.DueSoon, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sla, SupportSlaStatus.Overdue, StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownRequesterRole(string? role) =>
        string.IsNullOrWhiteSpace(role)
        || string.Equals(role, SwyftlyRoles.Buyer, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, SwyftlyRoles.Seller, StringComparison.OrdinalIgnoreCase)
        || IsSupportActorRole(role);

    private static bool TryParseCategory(string category, out SupportTicketCategory parsed) =>
        Enum.TryParse(category, ignoreCase: true, out parsed);

    private static async Task AddSupportAuditLogAsync(
        IAuditLogService auditLogService,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        string actionType,
        SupportTicket ticket,
        object? previousValue,
        string? reason,
        CancellationToken cancellationToken)
    {
        await auditLogService.RecordAsync(
            new CreateAuditLogEntry(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                GetSupportActorRole(principal),
                actionType,
                "SupportTicket",
                ticket.Id.ToString(),
                previousValue is null ? null : JsonSerializer.Serialize(previousValue),
                JsonSerializer.Serialize(SupportTicketAuditSnapshot.From(ticket)),
                reason,
                httpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
    }

    private static IResult InvalidCategory() =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["category"] = [$"Category must be one of: {string.Join(", ", Enum.GetNames<SupportTicketCategory>())}."]
        });

    private static IResult Validation(string key, string message) =>
        HttpResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [message]
        });

    private static IResult Conflict(string detail) =>
        HttpResults.Problem(title: "SupportTickets.InvalidState", detail: detail, statusCode: StatusCodes.Status409Conflict);

    private static IResult LinkedRecordNotFound(string recordType) =>
        HttpResults.Problem(
            title: $"SupportTickets.Linked{recordType}NotFound",
            detail: $"Linked {recordType.ToLowerInvariant()} was not found or is not available to the authenticated user.",
            statusCode: StatusCodes.Status404NotFound);

    private static IResult BuyerNotFound() =>
        HttpResults.Problem("The authenticated user does not have a buyer profile.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.BuyerNotFound");

    private static IResult SellerNotFound() =>
        HttpResults.Problem("The authenticated user does not have a seller profile.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.SellerNotFound");

    private static IResult TicketNotFound() =>
        HttpResults.Problem("Support ticket was not found.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.TicketNotFound");

    private static IResult SavedViewNotFound() =>
        HttpResults.Problem("Support saved view was not found.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.SavedViewNotFound");

    private static IResult UserNotFound() =>
        HttpResults.Problem("The authenticated user id could not be resolved.", statusCode: StatusCodes.Status404NotFound, title: "SupportTickets.UserNotFound");
}

public sealed record CreateSupportTicketRequest(
    string Category,
    string Subject,
    string Description,
    Guid? LinkedOrderId,
    Guid? LinkedProductId,
    Guid? LinkedSellerId,
    Guid? LinkedPaymentId);

public sealed record SupportMessageRequest(string Message);

public sealed record SupportTicketTriageRequest(string Priority, string? InternalNote);

public sealed record SupportTicketEscalationRequest(string Reason);

public sealed record SupportTicketResponse(
    Guid SupportTicketId,
    Guid CreatedByUserId,
    string CreatedByRole,
    Guid? BuyerId,
    Guid? SellerId,
    string Category,
    string Status,
    string Priority,
    string Subject,
    string Description,
    Guid? LinkedOrderId,
    Guid? LinkedProductId,
    Guid? LinkedSellerId,
    Guid? LinkedPaymentId,
    Guid? AssignedSupportUserId,
    string? EscalationReason,
    DateTimeOffset? EscalatedAtUtc,
    Guid? EscalatedByUserId,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    IReadOnlyCollection<SupportMessageResponse> Messages,
    SupportTicketCustomerContextResponse? CustomerContext = null);

public sealed record SupportTicketCustomerContextResponse(
    SupportCustomerBuyerContextResponse? Buyer,
    SupportCustomerSellerContextResponse? Seller,
    SupportCustomerOrderContextResponse? Order,
    SupportCustomerPaymentContextResponse? Payment,
    SupportCustomerProductContextResponse? Product);

public sealed record SupportCustomerBuyerContextResponse(
    Guid BuyerId,
    Guid UserId,
    string? DisplayName,
    string? Email,
    string? PhoneNumber);

public sealed record SupportCustomerSellerContextResponse(
    Guid SellerId,
    Guid UserId,
    string? DisplayName,
    string? ContactEmail,
    string? PhoneNumber,
    string VerificationStatus,
    string AdminRoute);

public sealed record SupportCustomerOrderContextResponse(
    Guid OrderId,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc,
    Guid BuyerId,
    Guid SellerId,
    string AdminRoute);

public sealed record SupportCustomerPaymentContextResponse(
    Guid PaymentId,
    Guid OrderId,
    string Provider,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? FailedAtUtc,
    string AdminRoute);

public sealed record SupportCustomerProductContextResponse(
    Guid ProductId,
    Guid SellerId,
    string? Title,
    string? Slug,
    string Status,
    string AdminRoute);

public sealed record SupportMessageResponse(
    Guid SupportMessageId,
    Guid SenderUserId,
    string SenderRole,
    string Message,
    bool IsInternal,
    DateTimeOffset CreatedAtUtc);

public sealed record SupportTicketQueueResponse(
    IReadOnlyCollection<SupportTicketQueueItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyCollection<SupportTicketCountResponse> StatusCounts,
    IReadOnlyCollection<SupportTicketCountResponse> PriorityCounts,
    IReadOnlyCollection<SupportTicketCountResponse> SlaCounts);

public sealed record SupportTicketQueueItemResponse(
    Guid SupportTicketId,
    Guid CreatedByUserId,
    string CreatedByRole,
    Guid? BuyerId,
    Guid? SellerId,
    string Category,
    string Status,
    string Priority,
    string Subject,
    string Description,
    Guid? LinkedOrderId,
    Guid? LinkedProductId,
    Guid? LinkedSellerId,
    Guid? LinkedPaymentId,
    Guid? AssignedSupportUserId,
    string? AssignedSupportDisplayName,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    string? EscalationReason,
    DateTimeOffset? EscalatedAtUtc,
    Guid? EscalatedByUserId,
    string? LatestInternalNote,
    int MessageCount,
    int AgeHours,
    string SlaStatus,
    DateTimeOffset SlaDueAtUtc);

public sealed record SupportTicketSummaryResponse(
    DateTimeOffset GeneratedAtUtc,
    int OpenTicketCount,
    int EscalatedTicketCount,
    int OverdueTicketCount,
    int MyOpenTicketCount,
    int UnassignedOpenTicketCount,
    int ResolvedTodayCount,
    int ResolvedLast7DaysCount,
    double? AverageFirstResponseHours,
    double? AverageResolutionHours,
    IReadOnlyCollection<SupportTicketCountResponse> StatusCounts,
    IReadOnlyCollection<SupportTicketCountResponse> PriorityCounts,
    IReadOnlyCollection<SupportTicketCountResponse> SlaCounts,
    IReadOnlyCollection<SupportTicketAssigneeCountResponse> AssigneeCounts);

public sealed record SupportTicketCountResponse(string Key, int Count);

public sealed record SupportTicketAssigneeCountResponse(string AssignedSupportUserId, string? AssignedSupportDisplayName, int Count);

public sealed record SupportTicketQualityReportResponse(
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string Bucket,
    SupportQualitySummaryResponse Summary,
    IReadOnlyCollection<SupportQualityTrendBucketResponse> Trend,
    IReadOnlyCollection<SupportQualityBreakdownResponse> CategoryBreakdown,
    IReadOnlyCollection<SupportQualityBreakdownResponse> PriorityBreakdown,
    IReadOnlyCollection<SupportQualityBreakdownResponse> RequesterRoleBreakdown,
    IReadOnlyCollection<SupportQualityBreakdownResponse> SlaBreakdown,
    IReadOnlyCollection<SupportQualityAssigneeBreakdownResponse> AssigneeBreakdown);

public sealed record SupportQualitySummaryResponse(
    int CreatedCount,
    int ResolvedCount,
    int ClosedCount,
    int EscalatedCount,
    int CurrentlyOpenCount,
    int CurrentlyOverdueCount,
    double? AverageFirstResponseHours,
    double? AverageResolutionHours,
    int FirstResponseTargetMetCount,
    int FirstResponseTargetMissedCount,
    int ResolutionTargetMetCount,
    int ResolutionTargetMissedCount);

public sealed record SupportQualityTrendBucketResponse(
    DateTimeOffset BucketStartUtc,
    DateTimeOffset BucketEndUtc,
    int CreatedCount,
    int ResolvedCount,
    int EscalatedCount,
    double? AverageFirstResponseHours,
    double? AverageResolutionHours);

public sealed record SupportQualityBreakdownResponse(
    string Key,
    int CreatedCount,
    int ResolvedCount,
    int EscalatedCount,
    int FirstResponseTargetMissedCount,
    int ResolutionTargetMissedCount,
    double? AverageFirstResponseHours,
    double? AverageResolutionHours);

public sealed record SupportQualityAssigneeBreakdownResponse(
    string AssignedSupportUserId,
    string? AssignedSupportDisplayName,
    int CreatedCount,
    int ResolvedCount,
    int EscalatedCount,
    int FirstResponseTargetMissedCount,
    int ResolutionTargetMissedCount,
    double? AverageFirstResponseHours,
    double? AverageResolutionHours);

public sealed record SupportSlaResponse(int AgeHours, string SlaStatus, DateTimeOffset SlaDueAtUtc);

public sealed class SupportOperationsOptions
{
    public const string SectionName = "SupportOperations";

    public int OpenSlaHours { get; init; } = 24;

    public int EscalatedSlaHours { get; init; } = 4;

    public int DueSoonHours { get; init; } = 4;
}

public sealed class SupportQualityOptions
{
    public const string SectionName = "SupportQuality";

    public int DefaultRangeDays { get; init; } = 30;

    public int MaxRangeDays { get; init; } = 366;

    public int FirstResponseTargetHours { get; init; } = 4;

    public int ResolutionTargetHours { get; init; } = 72;
}

public static class SupportSlaStatus
{
    public const string OnTrack = "OnTrack";
    public const string DueSoon = "DueSoon";
    public const string Overdue = "Overdue";
}

internal sealed record SupportQueueBuildResult(
    IReadOnlyCollection<SupportTicketQueueItemResponse> Items,
    IResult? Validation);

internal sealed record SupportQualityReportBuildResult(
    SupportTicketQualityReportResponse? Report,
    IResult? Validation);

internal sealed record SupportQualityTicket(
    Guid SupportTicketId,
    string Category,
    string Priority,
    string CreatedByRole,
    string Status,
    Guid? AssignedSupportUserId,
    string? AssignedSupportDisplayName,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ResolvedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    DateTimeOffset? EscalatedAtUtc,
    double? FirstResponseHours,
    double? ResolutionHours,
    bool? FirstResponseTargetMet,
    bool? ResolutionTargetMet,
    string SlaStatus);

internal sealed record SupportTicketAuditSnapshot(
    Guid SupportTicketId,
    string Status,
    string Priority,
    Guid? AssignedSupportUserId,
    string? EscalationReason,
    DateTimeOffset? EscalatedAtUtc,
    Guid? EscalatedByUserId,
    DateTimeOffset UpdatedAtUtc)
{
    public static SupportTicketAuditSnapshot From(SupportTicket ticket) =>
        new(
            ticket.Id,
            ticket.Status.ToString(),
            ticket.Priority.ToString(),
            ticket.AssignedSupportUserId,
            ticket.EscalationReason,
            ticket.EscalatedAtUtc,
            ticket.EscalatedByUserId,
            ticket.UpdatedAtUtc);
}
