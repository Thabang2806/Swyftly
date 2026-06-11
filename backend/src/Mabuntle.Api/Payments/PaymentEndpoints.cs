using System.Buffers;
using System.Text;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Mabuntle.Api.Results;
using Mabuntle.Api.Security;
using Mabuntle.Application.Identity;
using Mabuntle.Application.Payments;
using Mabuntle.Domain.Buyers;
using Mabuntle.Domain.Payments;
using Mabuntle.Infrastructure.Payments;
using Mabuntle.Infrastructure.Persistence;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Mabuntle.Api.Payments;

public static class PaymentEndpoints
{
    private const long MaxWebhookPayloadBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var buyerGroup = app.MapGroup("/api/payments")
            .WithTags("Payments")
            .RequireAuthorization(MabuntlePolicies.BuyerOnly);

        buyerGroup.MapPost("/initiate", InitiatePaymentAsync)
            .WithName("InitiatePayment")
            .WithSummary("Creates a local payment record and initializes the configured payment provider.")
            .RequireRateLimiting(MabuntleRateLimitPolicies.Payment)
            .Produces<PaymentInitiationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapPost("/api/payments/webhook/{provider}", ProcessWebhookAsync)
            .WithTags("Payment Webhooks")
            .WithName("ProcessPaymentWebhook")
            .WithSummary("Processes a payment provider webhook with provider signature verification and idempotency.")
            .AllowAnonymous()
            .RequireRateLimiting(MabuntleRateLimitPolicies.Webhook)
            .Produces<PaymentWebhookProcessingResult>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        app.MapGet("/api/payments/payfast/checkout/{providerReference}", GetPayFastCheckoutAsync)
            .WithTags("Payments")
            .WithName("GetPayFastCheckout")
            .WithSummary("Builds the PayFast hosted-checkout form for a pending local PayFast payment.")
            .AllowAnonymous()
            .RequireRateLimiting(MabuntleRateLimitPolicies.Payment)
            .Produces(StatusCodes.Status200OK, contentType: "text/html")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> InitiatePaymentAsync(
        InitiatePaymentApiRequest request,
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        IPaymentService paymentService,
        CancellationToken cancellationToken)
    {
        var buyer = await GetCurrentBuyerAsync(principal, dbContext, cancellationToken);
        if (buyer is null)
        {
            return BuyerNotFound();
        }

        var result = await paymentService.InitiatePaymentAsync(
            new InitiatePaymentRequest(buyer.Id, request.OrderId),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> ProcessWebhookAsync(
        string provider,
        HttpRequest httpRequest,
        IPaymentService paymentService,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedWebhookContentType(provider, httpRequest.ContentType))
        {
            return HttpResults.Problem(
                title: "Payments.InvalidWebhookContentType",
                detail: "Payment webhook content type is not supported for this provider.",
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        if (httpRequest.ContentLength > MaxWebhookPayloadBytes)
        {
            return WebhookPayloadTooLarge();
        }

        var payload = await ReadBodyWithLimitAsync(httpRequest.Body, MaxWebhookPayloadBytes, cancellationToken);
        if (payload is null)
        {
            return WebhookPayloadTooLarge();
        }

        var headers = httpRequest.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await paymentService.ProcessWebhookAsync(
            new ProcessPaymentWebhookRequest(provider, payload, headers),
            cancellationToken);

        return result.ToHttpResult(HttpResults.Ok);
    }

    private static async Task<IResult> GetPayFastCheckoutAsync(
        string providerReference,
        MabuntleDbContext dbContext,
        PayFastCheckoutFormBuilder formBuilder,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .SingleOrDefaultAsync(payment =>
                    payment.Provider == PayFastPaymentProvider.Name
                    && payment.ProviderReference == providerReference,
                cancellationToken);
        if (payment is null)
        {
            return HttpResults.Problem(
                title: "Payments.PayFastPaymentNotFound",
                detail: "The PayFast payment reference was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return HttpResults.Problem(
                title: "Payments.PayFastPaymentNotPending",
                detail: "Only pending PayFast payments can be checked out.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var order = await dbContext.Orders
            .SingleOrDefaultAsync(order => order.Id == payment.OrderId, cancellationToken);
        if (order is null)
        {
            return HttpResults.Problem(
                title: "Payments.OrderNotFound",
                detail: "The order for the PayFast payment was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var result = formBuilder.Build(payment, order);
        return result.ToHttpResult(form =>
            HttpResults.Content(form.Html, "text/html; charset=utf-8", Encoding.UTF8));
    }

    private static async Task<BuyerProfile?> GetCurrentBuyerAsync(
        ClaimsPrincipal principal,
        MabuntleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await dbContext.BuyerProfiles.SingleOrDefaultAsync(buyer => buyer.UserId == userId, cancellationToken)
            : null;
    }

    private static IResult BuyerNotFound() =>
        HttpResults.Problem(
            title: "Payments.BuyerNotFound",
            detail: "The authenticated user does not have a buyer profile.",
            statusCode: StatusCodes.Status404NotFound);

    private static bool IsJsonContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedWebhookContentType(string provider, string? contentType)
    {
        if (string.Equals(provider, PayFastPaymentProvider.Name, StringComparison.OrdinalIgnoreCase))
        {
            return IsFormUrlEncodedContentType(contentType);
        }

        return IsJsonContentType(contentType);
    }

    private static bool IsFormUrlEncodedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType.Split(';', 2)[0].Trim();
        return string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ReadBodyWithLimitAsync(
        Stream body,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            await using var memoryStream = new MemoryStream();
            while (true)
            {
                var read = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    return Encoding.UTF8.GetString(memoryStream.ToArray());
                }

                if (memoryStream.Length + read > maxBytes)
                {
                    return null;
                }

                await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static IResult WebhookPayloadTooLarge() =>
        HttpResults.Problem(
            title: "Payments.WebhookPayloadTooLarge",
            detail: $"Payment webhook payloads must be {MaxWebhookPayloadBytes} bytes or smaller.",
            statusCode: StatusCodes.Status413PayloadTooLarge);
}

public sealed record InitiatePaymentApiRequest(Guid OrderId);
