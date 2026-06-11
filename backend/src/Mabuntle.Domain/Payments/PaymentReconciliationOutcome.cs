namespace Mabuntle.Domain.Payments;

public enum PaymentReconciliationOutcome
{
    ProviderPending,
    MatchedNoAction,
    ProviderPaidMissingWebhook,
    ProviderFailedMissingWebhook,
    ManualRecoveryRequired
}
