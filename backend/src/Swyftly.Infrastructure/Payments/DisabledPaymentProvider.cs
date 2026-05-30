using Swyftly.Application.Common.Errors;
using Swyftly.Application.Common.Results;
using Swyftly.Application.Payments;

namespace Swyftly.Infrastructure.Payments;

public sealed class DisabledPaymentProvider : IPaymentProvider
{
    public const string Name = PaymentProviderNames.Disabled;

    private static readonly Error DisabledError = Error.Failure(
        "Payments.ProviderDisabled",
        "Online payments are not available yet. Please try again later.");

    public string ProviderName => Name;

    public Task<Result<PaymentInitiationResult>> InitializePaymentAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<PaymentInitiationResult>.Failure(DisabledError));

    public Task<Result<PaymentVerificationResult>> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<PaymentVerificationResult>.Failure(DisabledError));

    public Task<Result<PaymentWebhookEvent>> ParseWebhookAsync(
        PaymentWebhookParseRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<PaymentWebhookEvent>.Failure(DisabledError));

    public Task<Result<PaymentRefundResult>> RefundPaymentAsync(
        PaymentRefundRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<PaymentRefundResult>.Failure(DisabledError));

    public Task<Result> VerifyWebhookSignatureAsync(
        PaymentWebhookSignatureVerificationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(DisabledError));
}
