using Mabuntle.Application.Common.Errors;
using Mabuntle.Application.Common.Results;
using Mabuntle.Application.Common.Validation;

namespace Mabuntle.Application.Payments;

public interface IPaymentInitiationService
{
    Task<Result<PaymentInitiationResult>> InitiateAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PaymentInitiationService(IPaymentProvider paymentProvider) : IPaymentInitiationService
{
    public async Task<Result<PaymentInitiationResult>> InitiateAsync(
        PaymentInitiationRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = Validate(request);
        if (failures.Count > 0)
        {
            return Result<PaymentInitiationResult>.Failure(Error.Validation(failures));
        }

        return await paymentProvider.InitializePaymentAsync(request, cancellationToken);
    }

    private static List<ValidationFailure> Validate(PaymentInitiationRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.OrderId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("orderId", "Order id is required."));
        }

        if (request.BuyerId == Guid.Empty)
        {
            failures.Add(new ValidationFailure("buyerId", "Buyer id is required."));
        }

        if (request.Amount <= 0)
        {
            failures.Add(new ValidationFailure("amount", "Payment amount must be positive."));
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            failures.Add(new ValidationFailure("currency", "Currency is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            failures.Add(new ValidationFailure("description", "Description is required."));
        }

        if (!request.SuccessUrl.IsAbsoluteUri)
        {
            failures.Add(new ValidationFailure("successUrl", "Success URL must be absolute."));
        }

        if (!request.FailureUrl.IsAbsoluteUri)
        {
            failures.Add(new ValidationFailure("failureUrl", "Failure URL must be absolute."));
        }

        return failures;
    }
}
