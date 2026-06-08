using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pgvector;
using System.Text.Json;
using Swyftly.Domain.Advertising;
using Swyftly.Domain.Admin;
using Swyftly.Domain.Ai;
using Swyftly.Domain.Buyers;
using Swyftly.Domain.Carts;
using Swyftly.Domain.Catalog;
using Swyftly.Domain.Delivery;
using Swyftly.Domain.Disputes;
using Swyftly.Domain.Inventory;
using Swyftly.Domain.Ledger;
using Swyftly.Domain.Media;
using Swyftly.Domain.Notifications;
using Swyftly.Domain.Orders;
using Swyftly.Domain.Payments;
using Swyftly.Domain.Refunds;
using Swyftly.Domain.Returns;
using Swyftly.Domain.Sellers;
using Swyftly.Domain.Support;
using Swyftly.Infrastructure.Identity;

namespace Swyftly.Infrastructure.Persistence;

public sealed class SwyftlyDbContext(DbContextOptions<SwyftlyDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<BuyerProfile> BuyerProfiles => Set<BuyerProfile>();

    public DbSet<BuyerNotificationPreference> BuyerNotificationPreferences => Set<BuyerNotificationPreference>();

    public DbSet<BuyerDeliveryAddress> BuyerDeliveryAddresses => Set<BuyerDeliveryAddress>();

    public DbSet<BuyerWishlistItem> BuyerWishlistItems => Set<BuyerWishlistItem>();

    public DbSet<BuyerGrowthEvent> BuyerGrowthEvents => Set<BuyerGrowthEvent>();

    public DbSet<BuyerGrowthOutcome> BuyerGrowthOutcomes => Set<BuyerGrowthOutcome>();

    public DbSet<BuyerAiDiscoveryPreference> BuyerAiDiscoveryPreferences => Set<BuyerAiDiscoveryPreference>();

    public DbSet<BuyerAiDiscoveryHistory> BuyerAiDiscoveryHistory => Set<BuyerAiDiscoveryHistory>();

    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();

    public DbSet<SellerNotificationPreference> SellerNotificationPreferences => Set<SellerNotificationPreference>();

    public DbSet<SellerReportSchedule> SellerReportSchedules => Set<SellerReportSchedule>();

    public DbSet<SellerReportScheduleRun> SellerReportScheduleRuns => Set<SellerReportScheduleRun>();

    public DbSet<SellerFunnelEvent> SellerFunnelEvents => Set<SellerFunnelEvent>();

    public DbSet<SellerStorefront> SellerStorefronts => Set<SellerStorefront>();

    public DbSet<SellerStorePolicy> SellerStorePolicies => Set<SellerStorePolicy>();

    public DbSet<SellerAddress> SellerAddresses => Set<SellerAddress>();

    public DbSet<SellerDeliveryMethod> SellerDeliveryMethods => Set<SellerDeliveryMethod>();

    public DbSet<PickupPoint> PickupPoints => Set<PickupPoint>();

    public DbSet<SellerPayoutProfilePlaceholder> SellerPayoutProfiles => Set<SellerPayoutProfilePlaceholder>();

    public DbSet<SellerPayoutProfileChangeRequest> SellerPayoutProfileChangeRequests => Set<SellerPayoutProfileChangeRequest>();

    public DbSet<SellerVerification> SellerVerifications => Set<SellerVerification>();

    public DbSet<SellerVerificationEvidence> SellerVerificationEvidence => Set<SellerVerificationEvidence>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AdminQueueTriage> AdminQueueTriages => Set<AdminQueueTriage>();

    public DbSet<AdminQueueTriageNote> AdminQueueTriageNotes => Set<AdminQueueTriageNote>();

    public DbSet<AdminQueueSavedView> AdminQueueSavedViews => Set<AdminQueueSavedView>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<CategoryAttribute> CategoryAttributes => Set<CategoryAttribute>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductListingRevision> ProductListingRevisions => Set<ProductListingRevision>();

    public DbSet<ProductListingRevisionImage> ProductListingRevisionImages => Set<ProductListingRevisionImage>();

    public DbSet<ProductVariantRevision> ProductVariantRevisions => Set<ProductVariantRevision>();

    public DbSet<ProductVariantRevisionItem> ProductVariantRevisionItems => Set<ProductVariantRevisionItem>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MediaAssetVariant> MediaAssetVariants => Set<MediaAssetVariant>();

    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();

    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();

    public DbSet<AiProductSuggestion> AiProductSuggestions => Set<AiProductSuggestion>();

    public DbSet<AiSuggestionFieldAudit> AiSuggestionFieldAudits => Set<AiSuggestionFieldAudit>();

    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    public DbSet<AiPromptVersion> AiPromptVersions => Set<AiPromptVersion>();

    public DbSet<AiModerationResult> AiModerationResults => Set<AiModerationResult>();

    public DbSet<ProductEmbedding> ProductEmbeddings => Set<ProductEmbedding>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();

    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();

    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();

    public DbSet<ReturnMessage> ReturnMessages => Set<ReturnMessage>();

    public DbSet<ReturnRestockDecision> ReturnRestockDecisions => Set<ReturnRestockDecision>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<RefundEvent> RefundEvents => Set<RefundEvent>();

    public DbSet<Dispute> Disputes => Set<Dispute>();

    public DbSet<DisputeMessage> DisputeMessages => Set<DisputeMessage>();

    public DbSet<DisputeEvidence> DisputeEvidence => Set<DisputeEvidence>();

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    public DbSet<AdCampaign> AdCampaigns => Set<AdCampaign>();

    public DbSet<AdCampaignProduct> AdCampaignProducts => Set<AdCampaignProduct>();

    public DbSet<AdBudget> AdBudgets => Set<AdBudget>();

    public DbSet<AdImpression> AdImpressions => Set<AdImpression>();

    public DbSet<AdClick> AdClicks => Set<AdClick>();

    public DbSet<AdConversion> AdConversions => Set<AdConversion>();

    public DbSet<AdCharge> AdCharges => Set<AdCharge>();

    public DbSet<SellerAdCredit> SellerAdCredits => Set<SellerAdCredit>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    public DbSet<PaymentReconciliationReview> PaymentReconciliationReviews => Set<PaymentReconciliationReview>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<SellerBalance> SellerBalances => Set<SellerBalance>();

    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();

    public DbSet<SellerPayout> SellerPayouts => Set<SellerPayout>();

    public DbSet<SellerPayoutItem> SellerPayoutItems => Set<SellerPayoutItem>();

    public DbSet<SellerPayoutAdjustment> SellerPayoutAdjustments => Set<SellerPayoutAdjustment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<NotificationEmailDelivery> NotificationEmailDeliveries => Set<NotificationEmailDelivery>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, SwyftlyDbContextModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("mabuntle");
        modelBuilder.HasPostgresExtension("vector");
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BuyerProfile>(builder =>
        {
            builder.ToTable("buyer_profiles");
            builder.HasKey(profile => profile.Id);
            builder.HasIndex(profile => profile.UserId).IsUnique();
            builder.Property(profile => profile.DisplayName).HasMaxLength(BuyerProfile.DisplayNameMaxLength);
            builder.Property(profile => profile.PhoneNumber).HasMaxLength(BuyerProfile.PhoneNumberMaxLength);
            builder.Property(profile => profile.CreatedAtUtc).IsRequired();
            builder.Property(profile => profile.UpdatedAtUtc).IsRequired();
            builder.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<BuyerProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuyerNotificationPreference>(builder =>
        {
            builder.ToTable("buyer_notification_preferences");
            builder.HasKey(preference => preference.Id);
            builder.HasIndex(preference => new { preference.BuyerId, preference.Category }).IsUnique();
            builder.Property(preference => preference.Category).HasMaxLength(40).IsRequired();
            builder.Property(preference => preference.IsEnabled).IsRequired();
            builder.Property(preference => preference.EmailEnabled).IsRequired();
            builder.Property(preference => preference.CreatedAtUtc).IsRequired();
            builder.Property(preference => preference.UpdatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(preference => preference.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuyerGrowthEvent>(builder =>
        {
            builder.ToTable("buyer_growth_events");
            builder.HasKey(growthEvent => growthEvent.Id);
            builder.Property(growthEvent => growthEvent.EventType).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(growthEvent => growthEvent.SourceTool).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(growthEvent => growthEvent.ConfidenceBand).HasConversion<string>().HasMaxLength(20);
            builder.Property(growthEvent => growthEvent.FeedbackReason).HasConversion<string>().HasMaxLength(40);
            builder.Property(growthEvent => growthEvent.Category).HasMaxLength(BuyerGrowthEvent.ContextFieldMaxLength);
            builder.Property(growthEvent => growthEvent.Colour).HasMaxLength(BuyerGrowthEvent.ContextFieldMaxLength);
            builder.Property(growthEvent => growthEvent.Material).HasMaxLength(BuyerGrowthEvent.ContextFieldMaxLength);
            builder.Property(growthEvent => growthEvent.SourceRoute).HasMaxLength(BuyerGrowthEvent.SourceRouteMaxLength);
            builder.Property(growthEvent => growthEvent.OccurredAtUtc).IsRequired();
            builder.HasIndex(growthEvent => new { growthEvent.BuyerId, growthEvent.EventType, growthEvent.OccurredAtUtc });
            builder.HasIndex(growthEvent => new { growthEvent.EventType, growthEvent.SourceTool, growthEvent.OccurredAtUtc });
            builder.HasIndex(growthEvent => new { growthEvent.ProductId, growthEvent.EventType, growthEvent.OccurredAtUtc });
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(growthEvent => growthEvent.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(growthEvent => growthEvent.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BuyerGrowthOutcome>(builder =>
        {
            builder.ToTable("buyer_growth_outcomes");
            builder.HasKey(outcome => outcome.Id);
            builder.Property(outcome => outcome.OutcomeType).HasConversion<string>().HasMaxLength(50).IsRequired();
            builder.Property(outcome => outcome.SourceTool).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(outcome => outcome.ConfidenceBand).HasConversion<string>().HasMaxLength(20);
            builder.Property(outcome => outcome.OccurredAtUtc).IsRequired();
            builder.Property(outcome => outcome.AttributedFromUtc).IsRequired();
            builder.Property(outcome => outcome.AttributionWindowMinutes).IsRequired();
            builder.HasIndex(outcome => new { outcome.BuyerId, outcome.OutcomeType, outcome.OccurredAtUtc });
            builder.HasIndex(outcome => new { outcome.OutcomeType, outcome.SourceTool, outcome.OccurredAtUtc });
            builder.HasIndex(outcome => new { outcome.ProductId, outcome.OutcomeType, outcome.OccurredAtUtc });
            builder.HasIndex(outcome => new { outcome.CartId, outcome.OutcomeType });
            builder.HasIndex(outcome => new { outcome.OrderId, outcome.OutcomeType });
            builder.HasIndex(outcome => new { outcome.SourceEventId, outcome.OutcomeType })
                .IsUnique()
                .HasFilter("\"SourceEventId\" IS NOT NULL");
            builder.HasIndex(outcome => new
            {
                outcome.BuyerId,
                outcome.OutcomeType,
                outcome.SourceTool,
                outcome.ProductId,
                outcome.CartId,
                outcome.OrderId
            }).IsUnique();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(outcome => outcome.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<BuyerGrowthEvent>()
                .WithMany()
                .HasForeignKey(outcome => outcome.SourceEventId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(outcome => outcome.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Cart>()
                .WithMany()
                .HasForeignKey(outcome => outcome.CartId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(outcome => outcome.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BuyerAiDiscoveryPreference>(builder =>
        {
            builder.ToTable("buyer_ai_discovery_preferences");
            builder.HasKey(preference => preference.Id);
            builder.HasIndex(preference => preference.BuyerId).IsUnique();
            builder.Property(preference => preference.HistoryEnabled).IsRequired();
            builder.Property(preference => preference.PersonalizationEnabled).IsRequired();
            builder.Property(preference => preference.UpdatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(preference => preference.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuyerAiDiscoveryHistory>(builder =>
        {
            builder.ToTable("buyer_ai_discovery_history");
            builder.HasKey(history => history.Id);
            builder.Property(history => history.SourceTool).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(history => history.Category).HasMaxLength(Swyftly.Domain.Buyers.BuyerAiDiscoveryHistory.ContextFieldMaxLength);
            builder.Property(history => history.Colour).HasMaxLength(Swyftly.Domain.Buyers.BuyerAiDiscoveryHistory.ContextFieldMaxLength);
            builder.Property(history => history.Material).HasMaxLength(Swyftly.Domain.Buyers.BuyerAiDiscoveryHistory.ContextFieldMaxLength);
            builder.Property(history => history.ConfidenceBand).HasConversion<string>().HasMaxLength(20);
            builder.Property(history => history.ResultCount).IsRequired();
            builder.Property(history => history.ProductIds).HasColumnType("uuid[]");
            builder.Property(history => history.SourceRoute).HasMaxLength(Swyftly.Domain.Buyers.BuyerAiDiscoveryHistory.SourceRouteMaxLength);
            builder.Property(history => history.CreatedAtUtc).IsRequired();
            builder.HasIndex(history => new { history.BuyerId, history.CreatedAtUtc });
            builder.HasIndex(history => new { history.BuyerId, history.SourceTool, history.CreatedAtUtc });
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(history => history.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerNotificationPreference>(builder =>
        {
            builder.ToTable("seller_notification_preferences");
            builder.HasKey(preference => preference.Id);
            builder.HasIndex(preference => new { preference.SellerId, preference.Category }).IsUnique();
            builder.Property(preference => preference.Category).HasMaxLength(40).IsRequired();
            builder.Property(preference => preference.IsEnabled).IsRequired();
            builder.Property(preference => preference.EmailEnabled).IsRequired();
            builder.Property(preference => preference.CreatedAtUtc).IsRequired();
            builder.Property(preference => preference.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(preference => preference.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerReportSchedule>(builder =>
        {
            builder.ToTable("seller_report_schedules");
            builder.HasKey(schedule => schedule.Id);
            builder.HasIndex(schedule => schedule.SellerId).IsUnique();
            builder.HasIndex(schedule => schedule.NextRunAtUtc);
            builder.Property(schedule => schedule.Frequency).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(schedule => schedule.ReportRange).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(schedule => schedule.SendDayOfWeek).HasConversion<string>().HasMaxLength(20);
            builder.Property(schedule => schedule.SendTimeLocal).HasMaxLength(SellerReportSchedule.SendTimeLocalMaxLength).IsRequired();
            builder.Property(schedule => schedule.TimeZoneId).HasMaxLength(SellerReportSchedule.TimeZoneIdMaxLength).IsRequired();
            builder.Property(schedule => schedule.LastFailureReason).HasMaxLength(SellerReportSchedule.ErrorMaxLength);
            builder.Property(schedule => schedule.CreatedAtUtc).IsRequired();
            builder.Property(schedule => schedule.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(schedule => schedule.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerReportScheduleRun>(builder =>
        {
            builder.ToTable("seller_report_schedule_runs");
            builder.HasKey(run => run.Id);
            builder.HasIndex(run => new
            {
                run.SellerReportScheduleId,
                run.ReportPeriodStartUtc,
                run.ReportPeriodEndUtc
            }).IsUnique();
            builder.Property(run => run.FailureReason).HasMaxLength(SellerReportScheduleRun.FailureReasonMaxLength);
            builder.Property(run => run.CreatedAtUtc).IsRequired();
            builder.HasOne<SellerReportSchedule>()
                .WithMany()
                .HasForeignKey(run => run.SellerReportScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(run => run.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerFunnelEvent>(builder =>
        {
            builder.ToTable("seller_funnel_events");
            builder.Property(item => item.EventType).HasConversion<string>().HasMaxLength(40);
            builder.Property(item => item.HashedAnonymousVisitorId).HasMaxLength(SellerFunnelEvent.HashedVisitorIdMaxLength);
            builder.Property(item => item.SourceRoute).HasMaxLength(SellerFunnelEvent.SourceRouteMaxLength);
            builder.Property(item => item.IdempotencyKey).HasMaxLength(SellerFunnelEvent.IdempotencyKeyMaxLength);
            builder.Property(item => item.UtmSource).HasMaxLength(SellerFunnelEvent.UtmSourceMaxLength);
            builder.Property(item => item.UtmMedium).HasMaxLength(SellerFunnelEvent.UtmMediumMaxLength);
            builder.Property(item => item.UtmCampaign).HasMaxLength(SellerFunnelEvent.UtmCampaignMaxLength);
            builder.Property(item => item.ReferrerHost).HasMaxLength(SellerFunnelEvent.ReferrerHostMaxLength);
            builder.Property(item => item.SourceCategory).HasMaxLength(SellerFunnelEvent.SourceCategoryMaxLength);
            builder.HasIndex(item => new { item.SellerId, item.EventType, item.OccurredAtUtc });
            builder.HasIndex(item => new { item.SellerId, item.ProductId, item.EventType, item.OccurredAtUtc });
            builder.HasIndex(item => new { item.SellerId, item.SourceCategory, item.OccurredAtUtc });
            builder.HasIndex(item => new { item.SellerId, item.EventType, item.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(item => item.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Cart>()
                .WithMany()
                .HasForeignKey(item => item.CartId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(item => item.BuyerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BuyerDeliveryAddress>(builder =>
        {
            builder.ToTable("buyer_delivery_addresses");
            builder.HasKey(address => address.Id);
            builder.HasIndex(address => address.BuyerId);
            builder.HasIndex(address => new { address.BuyerId, address.IsDefault });
            builder.Property(address => address.Label).HasMaxLength(BuyerDeliveryAddress.LabelMaxLength).IsRequired();
            builder.Property(address => address.RecipientName).HasMaxLength(BuyerDeliveryAddress.RecipientNameMaxLength).IsRequired();
            builder.Property(address => address.PhoneNumber).HasMaxLength(BuyerDeliveryAddress.PhoneNumberMaxLength).IsRequired();
            builder.Property(address => address.AddressLine1).HasMaxLength(BuyerDeliveryAddress.AddressLineMaxLength).IsRequired();
            builder.Property(address => address.AddressLine2).HasMaxLength(BuyerDeliveryAddress.AddressLineMaxLength);
            builder.Property(address => address.Suburb).HasMaxLength(BuyerDeliveryAddress.SuburbMaxLength);
            builder.Property(address => address.City).HasMaxLength(BuyerDeliveryAddress.CityMaxLength).IsRequired();
            builder.Property(address => address.Province).HasMaxLength(BuyerDeliveryAddress.ProvinceMaxLength).IsRequired();
            builder.Property(address => address.PostalCode).HasMaxLength(BuyerDeliveryAddress.PostalCodeMaxLength).IsRequired();
            builder.Property(address => address.CountryCode).HasMaxLength(BuyerDeliveryAddress.CountryCodeLength).IsRequired();
            builder.Property(address => address.DeliveryInstructions).HasMaxLength(BuyerDeliveryAddress.DeliveryInstructionsMaxLength);
            builder.Property(address => address.VerificationStatus)
                .HasConversion<string>()
                .HasMaxLength(40)
                .HasDefaultValue(AddressVerificationStatus.Unverified)
                .IsRequired();
            builder.Property(address => address.VerificationProvider).HasMaxLength(BuyerDeliveryAddress.VerificationProviderMaxLength);
            builder.Property(address => address.VerificationWarningsJson).HasColumnType("jsonb");
            builder.Property(address => address.IsDefault).IsRequired();
            builder.Property(address => address.CreatedAtUtc).IsRequired();
            builder.Property(address => address.UpdatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(address => address.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PickupPoint>(builder =>
        {
            builder.ToTable("pickup_points");
            builder.HasKey(point => point.Id);
            builder.HasIndex(point => new { point.ProviderName, point.Code }).IsUnique();
            builder.HasIndex(point => new { point.CountryCode, point.Province, point.IsActive });
            builder.Property(point => point.ProviderName).HasMaxLength(PickupPoint.ProviderNameMaxLength).IsRequired();
            builder.Property(point => point.Code).HasMaxLength(PickupPoint.CodeMaxLength).IsRequired();
            builder.Property(point => point.Name).HasMaxLength(PickupPoint.NameMaxLength).IsRequired();
            builder.Property(point => point.AddressLine1).HasMaxLength(PickupPoint.AddressLineMaxLength).IsRequired();
            builder.Property(point => point.AddressLine2).HasMaxLength(PickupPoint.AddressLineMaxLength);
            builder.Property(point => point.Suburb).HasMaxLength(PickupPoint.SuburbMaxLength);
            builder.Property(point => point.City).HasMaxLength(PickupPoint.CityMaxLength).IsRequired();
            builder.Property(point => point.Province).HasMaxLength(PickupPoint.ProvinceMaxLength).IsRequired();
            builder.Property(point => point.PostalCode).HasMaxLength(PickupPoint.PostalCodeMaxLength).IsRequired();
            builder.Property(point => point.CountryCode).HasMaxLength(PickupPoint.CountryCodeLength).IsRequired();
            builder.Property(point => point.Latitude).HasPrecision(9, 6);
            builder.Property(point => point.Longitude).HasPrecision(9, 6);
            builder.Property(point => point.OpeningHours).HasMaxLength(PickupPoint.OpeningHoursMaxLength);
            builder.Property(point => point.IsActive).IsRequired();
            builder.Property(point => point.CreatedAtUtc).IsRequired();
            builder.Property(point => point.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<BuyerWishlistItem>(builder =>
        {
            builder.ToTable("buyer_wishlist_items");
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.BuyerId, item.ProductId }).IsUnique();
            builder.HasIndex(item => item.ProductId);
            builder.HasIndex(item => item.CreatedAtUtc);
            builder.Property(item => item.CreatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(item => item.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerProfile>(builder =>
        {
            builder.ToTable("seller_profiles");
            builder.HasKey(profile => profile.Id);
            builder.HasIndex(profile => profile.UserId).IsUnique();
            builder.Property(profile => profile.DisplayName).HasMaxLength(160);
            builder.Property(profile => profile.ContactEmail).HasMaxLength(320);
            builder.Property(profile => profile.PhoneNumber).HasMaxLength(64);
            builder.Property(profile => profile.BusinessType)
                .HasConversion<string>()
                .HasMaxLength(64);
            builder.Property(profile => profile.BusinessName).HasMaxLength(200);
            builder.Property(profile => profile.VerificationStatus)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(profile => profile.CreatedAtUtc).IsRequired();
            builder.Property(profile => profile.UpdatedAtUtc).IsRequired();
            builder.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<SellerProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerStorefront>(builder =>
        {
            builder.ToTable("seller_storefronts");
            builder.HasKey(storefront => storefront.Id);
            builder.HasIndex(storefront => storefront.SellerId).IsUnique();
            builder.HasIndex(storefront => storefront.Slug).IsUnique();
            builder.Property(storefront => storefront.StoreName).HasMaxLength(160).IsRequired();
            builder.Property(storefront => storefront.Slug).HasMaxLength(120).IsRequired();
            builder.Property(storefront => storefront.Description).HasMaxLength(1000);
            builder.Property(storefront => storefront.LogoUrl).HasMaxLength(2048);
            builder.Property(storefront => storefront.BannerUrl).HasMaxLength(2048);
            builder.Property(storefront => storefront.CreatedAtUtc).IsRequired();
            builder.Property(storefront => storefront.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithOne()
                .HasForeignKey<SellerStorefront>(storefront => storefront.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerStorePolicy>(builder =>
        {
            builder.ToTable("seller_store_policies");
            builder.HasKey(policy => policy.Id);
            builder.HasIndex(policy => policy.SellerId).IsUnique();
            builder.Property(policy => policy.ReturnWindowDays);
            builder.Property(policy => policy.ReturnPolicy).HasMaxLength(SellerStorePolicy.ReturnPolicyMaxLength);
            builder.Property(policy => policy.ExchangePolicy).HasMaxLength(SellerStorePolicy.ExchangePolicyMaxLength);
            builder.Property(policy => policy.FulfilmentPolicy).HasMaxLength(SellerStorePolicy.FulfilmentPolicyMaxLength);
            builder.Property(policy => policy.SupportPolicy).HasMaxLength(SellerStorePolicy.SupportPolicyMaxLength);
            builder.Property(policy => policy.CareInstructions).HasMaxLength(SellerStorePolicy.CareInstructionsMaxLength);
            builder.Property(policy => policy.ProductDisclaimer).HasMaxLength(SellerStorePolicy.ProductDisclaimerMaxLength);
            builder.Property(policy => policy.CreatedAtUtc).IsRequired();
            builder.Property(policy => policy.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithOne()
                .HasForeignKey<SellerStorePolicy>(policy => policy.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerAddress>(builder =>
        {
            builder.ToTable("seller_addresses");
            builder.HasKey(address => address.Id);
            builder.HasIndex(address => address.SellerId).IsUnique();
            builder.Property(address => address.AddressLine1).HasMaxLength(240).IsRequired();
            builder.Property(address => address.AddressLine2).HasMaxLength(240);
            builder.Property(address => address.City).HasMaxLength(120).IsRequired();
            builder.Property(address => address.Province).HasMaxLength(120).IsRequired();
            builder.Property(address => address.PostalCode).HasMaxLength(32).IsRequired();
            builder.Property(address => address.CountryCode).HasMaxLength(2).IsRequired();
            builder.Property(address => address.CreatedAtUtc).IsRequired();
            builder.Property(address => address.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithOne()
                .HasForeignKey<SellerAddress>(address => address.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerDeliveryMethod>(builder =>
        {
            builder.ToTable("seller_delivery_methods");
            builder.HasKey(method => method.Id);
            builder.HasIndex(method => method.SellerId);
            builder.HasIndex(method => new { method.SellerId, method.IsActive, method.DisplayOrder });
            builder.HasIndex(method => new { method.SellerId, method.CountryCode, method.Province });
            builder.Property(method => method.Name).HasMaxLength(SellerDeliveryMethod.NameMaxLength).IsRequired();
            builder.Property(method => method.Description).HasMaxLength(SellerDeliveryMethod.DescriptionMaxLength);
            builder.Property(method => method.MethodType)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();
            builder.Property(method => method.CountryCode)
                .HasMaxLength(SellerDeliveryMethod.CountryCodeLength)
                .IsRequired();
            builder.Property(method => method.Province).HasMaxLength(SellerDeliveryMethod.ProvinceMaxLength);
            builder.Property(method => method.BasePrice).HasPrecision(18, 2).IsRequired();
            builder.Property(method => method.FreeShippingThreshold).HasPrecision(18, 2);
            builder.Property(method => method.EstimatedMinDays).IsRequired();
            builder.Property(method => method.EstimatedMaxDays).IsRequired();
            builder.Property(method => method.DisplayOrder).IsRequired();
            builder.Property(method => method.IsActive).IsRequired();
            builder.Property(method => method.CreatedAtUtc).IsRequired();
            builder.Property(method => method.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(method => method.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerPayoutProfilePlaceholder>(builder =>
        {
            builder.ToTable("seller_payout_profiles");
            builder.HasKey(payoutProfile => payoutProfile.Id);
            builder.HasIndex(payoutProfile => payoutProfile.SellerId).IsUnique();
            builder.Property(payoutProfile => payoutProfile.PayoutProviderReference)
                .HasMaxLength(256)
                .IsRequired();
            builder.Property(payoutProfile => payoutProfile.CreatedAtUtc).IsRequired();
            builder.Property(payoutProfile => payoutProfile.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithOne()
                .HasForeignKey<SellerPayoutProfilePlaceholder>(payoutProfile => payoutProfile.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerPayoutProfileChangeRequest>(builder =>
        {
            builder.ToTable("seller_payout_profile_change_requests");
            builder.HasKey(request => request.Id);
            builder.HasIndex(request => request.SellerId)
                .HasFilter("\"Status\" IN ('Draft', 'PendingReview')")
                .IsUnique();
            builder.Property(request => request.ProposedPayoutProviderReference)
                .HasMaxLength(SellerPayoutProfileChangeRequest.PayoutProviderReferenceMaxLength)
                .IsRequired();
            builder.Property(request => request.Reason)
                .HasMaxLength(SellerPayoutProfileChangeRequest.ReasonMaxLength)
                .IsRequired();
            builder.Property(request => request.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(request => request.ReviewReason)
                .HasMaxLength(SellerPayoutProfileChangeRequest.ReasonMaxLength);
            builder.Property(request => request.ConcurrencyVersion)
                .IsConcurrencyToken()
                .IsRequired();
            builder.Property(request => request.CreatedAtUtc).IsRequired();
            builder.Property(request => request.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(request => request.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerVerification>(builder =>
        {
            builder.ToTable("seller_verifications");
            builder.HasKey(verification => verification.Id);
            builder.HasIndex(verification => verification.SellerId);
            builder.Property(verification => verification.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(verification => verification.RejectionReason).HasMaxLength(1000);
            builder.Property(verification => verification.CreatedAtUtc).IsRequired();
            builder.Property(verification => verification.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(verification => verification.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SellerVerificationEvidence>(builder =>
        {
            builder.ToTable("seller_verification_evidence");
            builder.HasKey(evidence => evidence.Id);
            builder.HasIndex(evidence => evidence.SellerId);
            builder.HasIndex(evidence => new { evidence.SellerId, evidence.EvidenceType });
            builder.HasIndex(evidence => evidence.RemovedAtUtc);
            builder.Property(evidence => evidence.EvidenceType)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(evidence => evidence.StorageProvider)
                .HasMaxLength(Swyftly.Domain.Sellers.SellerVerificationEvidence.StorageProviderMaxLength)
                .IsRequired();
            builder.Property(evidence => evidence.StorageKey)
                .HasMaxLength(Swyftly.Domain.Sellers.SellerVerificationEvidence.StorageKeyMaxLength)
                .IsRequired();
            builder.Property(evidence => evidence.OriginalFileName)
                .HasMaxLength(Swyftly.Domain.Sellers.SellerVerificationEvidence.OriginalFileNameMaxLength)
                .IsRequired();
            builder.Property(evidence => evidence.ContentType)
                .HasMaxLength(Swyftly.Domain.Sellers.SellerVerificationEvidence.ContentTypeMaxLength)
                .IsRequired();
            builder.Property(evidence => evidence.ByteSize).IsRequired();
            builder.Property(evidence => evidence.Sha256Hash)
                .HasMaxLength(Swyftly.Domain.Sellers.SellerVerificationEvidence.Sha256HashMaxLength)
                .IsRequired();
            builder.Property(evidence => evidence.Note)
                .HasMaxLength(Swyftly.Domain.Sellers.SellerVerificationEvidence.NoteMaxLength);
            builder.Property(evidence => evidence.UploadedByUserId).IsRequired();
            builder.Property(evidence => evidence.UploadedAtUtc).IsRequired();
            builder.Property(evidence => evidence.RemovedByUserId);
            builder.Property(evidence => evidence.RemovedAtUtc);
            builder.Property(evidence => evidence.CreatedAtUtc).IsRequired();
            builder.Property(evidence => evidence.UpdatedAtUtc).IsRequired();
            builder.Ignore(evidence => evidence.IsActive);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(evidence => evidence.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.ToTable("refresh_tokens");
            builder.HasKey(token => token.Id);
            builder.HasIndex(token => token.TokenHash).IsUnique();
            builder.HasIndex(token => token.UserId);
            builder.HasIndex(token => new { token.UserId, token.FamilyId });
            builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            builder.Property(token => token.FamilyId)
                .HasColumnName("family_id")
                .IsRequired();
            builder.Property(token => token.ReplacedByTokenHash).HasMaxLength(128);
            builder.Property(token => token.RevokedReason)
                .HasColumnName("revoked_reason")
                .HasMaxLength(120);

            builder.HasOne(token => token.User)
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("audit_logs");
            builder.HasKey(auditLog => auditLog.Id);
            builder.HasIndex(auditLog => new { auditLog.EntityType, auditLog.EntityId });
            builder.HasIndex(auditLog => auditLog.CreatedAtUtc);
            builder.Property(auditLog => auditLog.ActorUserId).HasMaxLength(64);
            builder.Property(auditLog => auditLog.ActorRole).HasMaxLength(64);
            builder.Property(auditLog => auditLog.ActionType).HasMaxLength(128).IsRequired();
            builder.Property(auditLog => auditLog.EntityType).HasMaxLength(128).IsRequired();
            builder.Property(auditLog => auditLog.EntityId).HasMaxLength(64);
            builder.Property(auditLog => auditLog.Reason).HasMaxLength(1000);
            builder.Property(auditLog => auditLog.IpAddress).HasMaxLength(64);
            builder.Property(auditLog => auditLog.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<AdminQueueTriage>(builder =>
        {
            builder.ToTable("admin_queue_triage");
            builder.HasKey(triage => triage.Id);
            builder.HasIndex(triage => new { triage.ItemType, triage.ItemId }).IsUnique();
            builder.HasIndex(triage => triage.AssignedToUserId);
            builder.HasIndex(triage => triage.Priority);
            builder.Property(triage => triage.ItemType).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(triage => triage.Priority).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(triage => triage.LatestNote).HasMaxLength(AdminQueueTriage.LatestNoteMaxLength);
            builder.Property(triage => triage.CreatedAtUtc).IsRequired();
            builder.Property(triage => triage.UpdatedAtUtc).IsRequired();
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(triage => triage.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasMany(triage => triage.Notes)
                .WithOne()
                .HasForeignKey(note => note.TriageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminQueueTriageNote>(builder =>
        {
            builder.ToTable("admin_queue_triage_notes");
            builder.HasKey(note => note.Id);
            builder.HasIndex(note => note.TriageId);
            builder.HasIndex(note => note.CreatedAtUtc);
            builder.Property(note => note.Note).HasMaxLength(AdminQueueTriageNote.NoteMaxLength).IsRequired();
            builder.Property(note => note.CreatedAtUtc).IsRequired();
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(note => note.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminQueueSavedView>(builder =>
        {
            builder.ToTable("admin_queue_saved_views");
            builder.HasKey(view => view.Id);
            builder.HasIndex(view => new { view.AdminUserId, view.Queue, view.Name }).IsUnique();
            builder.HasIndex(view => new { view.AdminUserId, view.Queue, view.IsDefault });
            builder.Property(view => view.Queue).HasMaxLength(AdminQueueSavedView.QueueMaxLength).IsRequired();
            builder.Property(view => view.Name).HasMaxLength(AdminQueueSavedView.NameMaxLength).IsRequired();
            builder.Property(view => view.IsDefault).IsRequired();
            builder.Property(view => view.View).HasMaxLength(AdminQueueSavedView.ShortFilterMaxLength);
            builder.Property(view => view.Status).HasMaxLength(AdminQueueSavedView.ShortFilterMaxLength);
            builder.Property(view => view.Category).HasMaxLength(AdminQueueSavedView.ShortFilterMaxLength);
            builder.Property(view => view.Search).HasMaxLength(AdminQueueSavedView.SearchMaxLength);
            builder.Property(view => view.Assigned).HasMaxLength(AdminQueueSavedView.ShortFilterMaxLength);
            builder.Property(view => view.Priority).HasMaxLength(AdminQueueSavedView.ShortFilterMaxLength);
            builder.Property(view => view.Sla).HasMaxLength(AdminQueueSavedView.ShortFilterMaxLength);
            builder.Property(view => view.Sort).HasMaxLength(AdminQueueSavedView.SortMaxLength);
            builder.Property(view => view.PageSize).IsRequired();
            builder.Property(view => view.CreatedAtUtc).IsRequired();
            builder.Property(view => view.UpdatedAtUtc).IsRequired();
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(view => view.AdminUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Category>(builder =>
        {
            builder.ToTable("categories");
            builder.HasKey(category => category.Id);
            builder.HasIndex(category => category.Slug).IsUnique();
            builder.HasIndex(category => new { category.ParentCategoryId, category.DisplayOrder });
            builder.Property(category => category.Name).HasMaxLength(160).IsRequired();
            builder.Property(category => category.Slug).HasMaxLength(180).IsRequired();
            builder.Property(category => category.DisplayOrder).IsRequired();
            builder.Property(category => category.IsActive).IsRequired();
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(category => category.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasData(CatalogSeedData.CreateCategories());
        });

        modelBuilder.Entity<CategoryAttribute>(builder =>
        {
            builder.ToTable("category_attributes");
            builder.HasKey(attribute => attribute.Id);
            builder.HasIndex(attribute => new { attribute.CategoryId, attribute.Key }).IsUnique();
            builder.HasIndex(attribute => new { attribute.CategoryId, attribute.DisplayOrder });
            builder.Property(attribute => attribute.Name).HasMaxLength(160).IsRequired();
            builder.Property(attribute => attribute.Key).HasMaxLength(120).IsRequired();
            builder.Property(attribute => attribute.DataType)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(attribute => attribute.IsRequired).IsRequired();
            builder.Property(attribute => attribute.AllowedValuesJson)
                .HasColumnName("allowed_values_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(attribute => attribute.DisplayOrder).IsRequired();
            builder.Property(attribute => attribute.IsActive).IsRequired();
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(attribute => attribute.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasData(CatalogSeedData.CreateCategoryAttributes());
        });

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("products");
            builder.HasKey(product => product.Id);
            builder.HasIndex(product => new { product.SellerId, product.Slug })
                .IsUnique()
                .HasFilter("\"Slug\" IS NOT NULL");
            builder.HasIndex(product => product.CategoryId);
            builder.HasIndex(product => product.Status);
            builder.Property(product => product.Title).HasMaxLength(200);
            builder.Property(product => product.Slug).HasMaxLength(220);
            builder.Property(product => product.ShortDescription).HasMaxLength(500);
            builder.Property(product => product.FullDescription).HasMaxLength(5000);
            builder.Property(product => product.SeoTitle).HasMaxLength(Product.SeoTitleMaxLength);
            builder.Property(product => product.SeoDescription).HasMaxLength(Product.SeoDescriptionMaxLength);
            builder.Property(product => product.MerchandisingLabel).HasMaxLength(Product.MerchandisingLabelMaxLength);
            builder.Property(product => product.CareInstructions).HasMaxLength(Product.CareInstructionsMaxLength);
            builder.Property(product => product.ProductDisclaimer).HasMaxLength(Product.ProductDisclaimerMaxLength);
            builder.Property(product => product.TagsJson)
                .HasColumnName("tags_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(product => product.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(product => product.RejectionReason).HasMaxLength(1000);
            builder.Property(product => product.CreatedAtUtc).IsRequired();
            builder.Property(product => product.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(product => product.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductVariant>(builder =>
        {
            builder.ToTable("product_variants", table =>
            {
                table.HasCheckConstraint(
                    "CK_product_variants_reserved_quantity_non_negative",
                    "\"ReservedQuantity\" >= 0");
                table.HasCheckConstraint(
                    "CK_product_variants_reserved_quantity_not_above_stock",
                    "\"ReservedQuantity\" <= \"StockQuantity\"");
            });
            builder.HasKey(variant => variant.Id);
            builder.HasIndex(variant => new { variant.ProductId, variant.Sku }).IsUnique();
            builder.HasIndex(variant => new { variant.ProductId, variant.Size, variant.Colour }).IsUnique();
            builder.HasIndex(variant => variant.Status);
            builder.Property(variant => variant.Sku).HasMaxLength(120).IsRequired();
            builder.Property(variant => variant.Size).HasMaxLength(64).IsRequired();
            builder.Property(variant => variant.Colour).HasMaxLength(64).IsRequired();
            builder.Property(variant => variant.Price).HasPrecision(18, 2).IsRequired();
            builder.Property(variant => variant.CompareAtPrice).HasPrecision(18, 2);
            builder.Property(variant => variant.StockQuantity).IsRequired();
            builder.Property(variant => variant.ReservedQuantity).IsRequired();
            builder.Property(variant => variant.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(variant => variant.Barcode).HasMaxLength(120);
            builder.Property(variant => variant.CreatedAtUtc).IsRequired();
            builder.Property(variant => variant.UpdatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(variant => variant.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductImage>(builder =>
        {
            builder.ToTable("product_images");
            builder.HasKey(image => image.Id);
            builder.HasIndex(image => new { image.ProductId, image.IsPrimary })
                .IsUnique()
                .HasFilter("\"IsPrimary\" = TRUE");
            builder.HasIndex(image => new { image.ProductId, image.SortOrder });
            builder.HasIndex(image => image.MediaAssetId);
            builder.Property(image => image.MediaAssetId);
            builder.Property(image => image.Url).HasMaxLength(2048).IsRequired();
            builder.Property(image => image.StorageKey).HasMaxLength(512).IsRequired();
            builder.Property(image => image.AltText).HasMaxLength(300);
            builder.Property(image => image.SortOrder).IsRequired();
            builder.Property(image => image.IsPrimary).IsRequired();
            builder.Property(image => image.CreatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(image => image.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<MediaAsset>()
                .WithMany()
                .HasForeignKey(image => image.MediaAssetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductListingRevision>(builder =>
        {
            builder.ToTable("product_listing_revisions");
            builder.HasKey(revision => revision.Id);
            builder.HasIndex(revision => revision.ProductId);
            builder.HasIndex(revision => revision.SellerId);
            builder.HasIndex(revision => revision.Status);
            builder.HasIndex(revision => new { revision.ProductId, revision.Status })
                .IsUnique()
                .HasFilter("\"Status\" IN ('Draft', 'PendingReview', 'Rejected')");
            builder.Property(revision => revision.Title).HasMaxLength(200);
            builder.Property(revision => revision.Slug).HasMaxLength(220);
            builder.Property(revision => revision.ShortDescription).HasMaxLength(500);
            builder.Property(revision => revision.FullDescription).HasMaxLength(5000);
            builder.Property(revision => revision.SeoTitle).HasMaxLength(Product.SeoTitleMaxLength);
            builder.Property(revision => revision.SeoDescription).HasMaxLength(Product.SeoDescriptionMaxLength);
            builder.Property(revision => revision.MerchandisingLabel).HasMaxLength(Product.MerchandisingLabelMaxLength);
            builder.Property(revision => revision.CareInstructions).HasMaxLength(Product.CareInstructionsMaxLength);
            builder.Property(revision => revision.ProductDisclaimer).HasMaxLength(Product.ProductDisclaimerMaxLength);
            builder.Property(revision => revision.TagsJson)
                .HasColumnName("tags_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(revision => revision.AttributesJson)
                .HasColumnName("attributes_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(revision => revision.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(revision => revision.RejectionReason).HasMaxLength(1000);
            builder.Property(revision => revision.CreatedAtUtc).IsRequired();
            builder.Property(revision => revision.UpdatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(revision => revision.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(revision => revision.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductListingRevisionImage>(builder =>
        {
            builder.ToTable("product_listing_revision_images");
            builder.HasKey(image => image.Id);
            builder.HasIndex(image => new { image.RevisionId, image.IsPrimary })
                .IsUnique()
                .HasFilter("\"IsPrimary\" = TRUE");
            builder.HasIndex(image => new { image.RevisionId, image.SortOrder });
            builder.HasIndex(image => image.MediaAssetId);
            builder.Property(image => image.MediaAssetId);
            builder.Property(image => image.Url).HasMaxLength(2048).IsRequired();
            builder.Property(image => image.StorageKey).HasMaxLength(512).IsRequired();
            builder.Property(image => image.AltText).HasMaxLength(300);
            builder.Property(image => image.SortOrder).IsRequired();
            builder.Property(image => image.IsPrimary).IsRequired();
            builder.Property(image => image.CreatedAtUtc).IsRequired();
            builder.HasOne<ProductListingRevision>()
                .WithMany()
                .HasForeignKey(image => image.RevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<MediaAsset>()
                .WithMany()
                .HasForeignKey(image => image.MediaAssetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductVariantRevision>(builder =>
        {
            builder.ToTable("product_variant_revisions");
            builder.HasKey(revision => revision.Id);
            builder.HasIndex(revision => revision.ProductId);
            builder.HasIndex(revision => revision.SellerId);
            builder.HasIndex(revision => revision.Status);
            builder.HasIndex(revision => new { revision.ProductId, revision.Status })
                .IsUnique()
                .HasFilter("\"Status\" IN ('Draft', 'PendingReview', 'Rejected')");
            builder.Property(revision => revision.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(revision => revision.SellerReason).HasMaxLength(1000);
            builder.Property(revision => revision.RejectionReason).HasMaxLength(1000);
            builder.Property(revision => revision.CreatedAtUtc).IsRequired();
            builder.Property(revision => revision.UpdatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(revision => revision.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(revision => revision.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductVariantRevisionItem>(builder =>
        {
            builder.ToTable("product_variant_revision_items");
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => item.RevisionId);
            builder.HasIndex(item => item.SourceVariantId);
            builder.HasIndex(item => new { item.RevisionId, item.Sku });
            builder.HasIndex(item => new { item.RevisionId, item.Size, item.Colour });
            builder.Property(item => item.Operation)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(item => item.Sku).HasMaxLength(120).IsRequired();
            builder.Property(item => item.Size).HasMaxLength(64).IsRequired();
            builder.Property(item => item.Colour).HasMaxLength(64).IsRequired();
            builder.Property(item => item.Price).HasPrecision(18, 2).IsRequired();
            builder.Property(item => item.CompareAtPrice).HasPrecision(18, 2);
            builder.Property(item => item.ProposedStatus)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(item => item.Barcode).HasMaxLength(120);
            builder.HasOne<ProductVariantRevision>()
                .WithMany()
                .HasForeignKey(item => item.RevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(item => item.SourceVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MediaAsset>(builder =>
        {
            builder.ToTable("media_assets");
            builder.HasKey(asset => asset.Id);
            builder.HasIndex(asset => asset.SellerId);
            builder.HasIndex(asset => asset.ProductId);
            builder.HasIndex(asset => asset.ProductListingRevisionId);
            builder.HasIndex(asset => asset.LifecycleStatus);
            builder.HasIndex(asset => asset.ScanStatus);
            builder.HasIndex(asset => asset.StorageKey).IsUnique();
            builder.Property(asset => asset.Provider).HasMaxLength(64).IsRequired();
            builder.Property(asset => asset.Bucket).HasMaxLength(255).IsRequired();
            builder.Property(asset => asset.StorageKey).HasMaxLength(700).IsRequired();
            builder.Property(asset => asset.PublicUrl).HasMaxLength(2048).IsRequired();
            builder.Property(asset => asset.OriginalFileName).HasMaxLength(255).IsRequired();
            builder.Property(asset => asset.ContentType).HasMaxLength(100).IsRequired();
            builder.Property(asset => asset.Sha256Hash).HasMaxLength(128).IsRequired();
            builder.Property(asset => asset.ScanStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(asset => asset.LifecycleStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(asset => asset.LastError).HasMaxLength(500);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(asset => asset.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(asset => asset.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ProductListingRevision>()
                .WithMany()
                .HasForeignKey(asset => asset.ProductListingRevisionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MediaAssetVariant>(builder =>
        {
            builder.ToTable("media_asset_variants");
            builder.HasKey(variant => variant.Id);
            builder.HasIndex(variant => new { variant.MediaAssetId, variant.Kind }).IsUnique();
            builder.HasIndex(variant => variant.StorageKey).IsUnique();
            builder.Property(variant => variant.Kind)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(variant => variant.StorageKey).HasMaxLength(700).IsRequired();
            builder.Property(variant => variant.PublicUrl).HasMaxLength(2048).IsRequired();
            builder.Property(variant => variant.ContentType).HasMaxLength(100).IsRequired();
            builder.HasOne<MediaAsset>()
                .WithMany()
                .HasForeignKey(variant => variant.MediaAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductAttributeValue>(builder =>
        {
            builder.ToTable("product_attribute_values");
            builder.HasKey(attribute => attribute.Id);
            builder.HasIndex(attribute => new { attribute.ProductId, attribute.Key }).IsUnique();
            builder.Property(attribute => attribute.Key).HasMaxLength(120).IsRequired();
            builder.Property(attribute => attribute.ValueJson)
                .HasColumnName("value_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(attribute => attribute.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductReview>(builder =>
        {
            builder.ToTable("product_reviews");
            builder.HasKey(review => review.Id);
            builder.HasIndex(review => review.BuyerId);
            builder.HasIndex(review => review.SellerId);
            builder.HasIndex(review => review.ProductId);
            builder.HasIndex(review => review.OrderId);
            builder.HasIndex(review => review.OrderItemId).IsUnique();
            builder.HasIndex(review => new { review.ProductId, review.Status, review.CreatedAtUtc });
            builder.Property(review => review.Rating).IsRequired();
            builder.Property(review => review.Title).HasMaxLength(160);
            builder.Property(review => review.Body).HasMaxLength(2000);
            builder.Property(review => review.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(review => review.ModerationReason).HasMaxLength(1000);
            builder.Property(review => review.CreatedAtUtc).IsRequired();
            builder.Property(review => review.UpdatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(review => review.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(review => review.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(review => review.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(review => review.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OrderItem>()
                .WithMany()
                .HasForeignKey(review => review.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiProductSuggestion>(builder =>
        {
            builder.ToTable("ai_product_suggestions");
            builder.HasKey(suggestion => suggestion.Id);
            builder.HasIndex(suggestion => suggestion.SellerId);
            builder.HasIndex(suggestion => suggestion.ProductId);
            builder.HasIndex(suggestion => suggestion.Status);
            builder.HasIndex(suggestion => suggestion.CreatedAtUtc);
            builder.Property(suggestion => suggestion.InputNotes).HasMaxLength(4000);
            builder.Property(suggestion => suggestion.InputImageIdsJson)
                .HasColumnName("input_image_ids_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(suggestion => suggestion.SuggestedTitle).HasMaxLength(200);
            builder.Property(suggestion => suggestion.SuggestedShortDescription).HasMaxLength(500);
            builder.Property(suggestion => suggestion.SuggestedFullDescription).HasMaxLength(5000);
            builder.Property(suggestion => suggestion.SuggestedCategoryPath).HasMaxLength(500);
            builder.Property(suggestion => suggestion.SuggestedAttributesJson)
                .HasColumnName("suggested_attributes_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(suggestion => suggestion.SuggestedTagsJson)
                .HasColumnName("suggested_tags_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(suggestion => suggestion.MissingFieldsJson)
                .HasColumnName("missing_fields_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(suggestion => suggestion.RiskFlagsJson)
                .HasColumnName("risk_flags_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(suggestion => suggestion.QualityScore)
                .HasPrecision(5, 2)
                .IsRequired();
            builder.Property(suggestion => suggestion.ModelUsed).HasMaxLength(128).IsRequired();
            builder.Property(suggestion => suggestion.PromptVersion).HasMaxLength(64).IsRequired();
            builder.Property(suggestion => suggestion.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(suggestion => suggestion.CreatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(suggestion => suggestion.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(suggestion => suggestion.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiSuggestionFieldAudit>(builder =>
        {
            builder.ToTable("ai_suggestion_field_audits");
            builder.HasKey(audit => audit.Id);
            builder.HasIndex(audit => audit.SuggestionId);
            builder.Property(audit => audit.FieldName).HasMaxLength(128).IsRequired();
            builder.Property(audit => audit.AiValue)
                .HasColumnName("ai_value")
                .HasColumnType("jsonb");
            builder.Property(audit => audit.SellerFinalValue)
                .HasColumnName("seller_final_value")
                .HasColumnType("jsonb");
            builder.Property(audit => audit.WasAccepted).IsRequired();
            builder.Property(audit => audit.WasEdited).IsRequired();
            builder.Property(audit => audit.CreatedAtUtc).IsRequired();
            builder.HasOne<AiProductSuggestion>()
                .WithMany()
                .HasForeignKey(audit => audit.SuggestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiUsageLog>(builder =>
        {
            builder.ToTable("ai_usage_logs");
            builder.HasKey(log => log.Id);
            builder.HasIndex(log => log.FeatureName);
            builder.HasIndex(log => log.UserId);
            builder.HasIndex(log => log.SellerId);
            builder.HasIndex(log => log.CreatedAtUtc);
            builder.Property(log => log.FeatureName).HasMaxLength(128).IsRequired();
            builder.Property(log => log.UserId).HasMaxLength(64).IsRequired();
            builder.Property(log => log.ModelUsed).HasMaxLength(128).IsRequired();
            builder.Property(log => log.CostEstimate).HasPrecision(18, 6);
            builder.Property(log => log.ErrorMessage).HasMaxLength(2000);
            builder.Property(log => log.CreatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(log => log.SellerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AiPromptVersion>(builder =>
        {
            builder.ToTable("ai_prompt_versions");
            builder.HasKey(promptVersion => promptVersion.Id);
            builder.HasIndex(promptVersion => new { promptVersion.FeatureName, promptVersion.Version }).IsUnique();
            builder.Property(promptVersion => promptVersion.FeatureName).HasMaxLength(128).IsRequired();
            builder.Property(promptVersion => promptVersion.Version).HasMaxLength(64).IsRequired();
            builder.Property(promptVersion => promptVersion.PromptTemplate).IsRequired();
            builder.Property(promptVersion => promptVersion.IsActive).IsRequired();
            builder.Property(promptVersion => promptVersion.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<AiModerationResult>(builder =>
        {
            builder.ToTable("ai_moderation_results");
            builder.HasKey(result => result.Id);
            builder.HasIndex(result => result.ProductId);
            builder.HasIndex(result => result.SellerId);
            builder.HasIndex(result => result.NeedsAdminReview);
            builder.HasIndex(result => result.CreatedAtUtc);
            builder.Property(result => result.RiskLevel)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(result => result.Reason).HasMaxLength(1000).IsRequired();
            builder.Property(result => result.DetectedTermsJson)
                .HasColumnName("detected_terms_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(result => result.MissingFieldsJson)
                .HasColumnName("missing_fields_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(result => result.FlagsJson)
                .HasColumnName("flags_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(result => result.Provider).HasMaxLength(128).IsRequired();
            builder.Property(result => result.CreatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(result => result.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(result => result.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductEmbedding>(builder =>
        {
            builder.ToTable("product_embeddings");
            builder.HasKey(embedding => embedding.Id);
            builder.HasIndex(embedding => embedding.ProductId);
            builder.HasIndex(embedding => embedding.ModelUsed);
            builder.HasIndex(embedding => embedding.CreatedAtUtc);
            builder.HasIndex(embedding => new { embedding.ProductId, embedding.ModelUsed }).IsUnique();
            builder.Property(embedding => embedding.SourceText).HasMaxLength(12000).IsRequired();
            var embeddingProperty = builder.Property(embedding => embedding.Embedding).IsRequired();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                embeddingProperty.HasConversion(
                    embedding => JsonSerializer.Serialize(embedding.ToArray(), (JsonSerializerOptions?)null),
                    json => new Vector(JsonSerializer.Deserialize<float[]>(json, (JsonSerializerOptions?)null) ?? Array.Empty<float>()));
            }
            else
            {
                embeddingProperty.HasColumnType($"vector({ProductEmbedding.EmbeddingDimension})");
            }

            builder.Property(embedding => embedding.ModelUsed).HasMaxLength(128).IsRequired();
            builder.Property(embedding => embedding.CreatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(embedding => embedding.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Cart>(builder =>
        {
            builder.ToTable("carts");
            builder.HasKey(cart => cart.Id);
            builder.Property(cart => cart.Id).ValueGeneratedNever();
            builder.HasIndex(cart => cart.BuyerId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'");
            builder.HasIndex(cart => cart.SellerId);
            builder.Property(cart => cart.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(cart => cart.CreatedAtUtc).IsRequired();
            builder.Property(cart => cart.UpdatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(cart => cart.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(cart => cart.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(cart => cart.Items)
                .WithOne()
                .HasForeignKey(item => item.CartId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(Cart.Items))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<CartItem>(builder =>
        {
            builder.ToTable("cart_items");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.HasIndex(item => item.CartId);
            builder.HasIndex(item => item.ProductVariantId);
            builder.HasIndex(item => new { item.CartId, item.ProductVariantId }).IsUnique();
            builder.Property(item => item.ProductTitle).HasMaxLength(200);
            builder.Property(item => item.Sku).HasMaxLength(120).IsRequired();
            builder.Property(item => item.Size).HasMaxLength(64).IsRequired();
            builder.Property(item => item.Colour).HasMaxLength(64).IsRequired();
            builder.Property(item => item.UnitPrice).HasPrecision(18, 2).IsRequired();
            builder.Property(item => item.Quantity).IsRequired();
            builder.Property(item => item.CreatedAtUtc).IsRequired();
            builder.Property(item => item.UpdatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(item => item.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryReservation>(builder =>
        {
            builder.ToTable("inventory_reservations");
            builder.HasKey(reservation => reservation.Id);
            builder.HasIndex(reservation => reservation.ProductVariantId);
            builder.HasIndex(reservation => reservation.BuyerId);
            builder.HasIndex(reservation => reservation.CartId);
            builder.HasIndex(reservation => reservation.Status);
            builder.HasIndex(reservation => reservation.ExpiresAtUtc);
            builder.Property(reservation => reservation.Quantity).IsRequired();
            builder.Property(reservation => reservation.ExpiresAtUtc).IsRequired();
            builder.Property(reservation => reservation.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(reservation => reservation.CreatedAtUtc).IsRequired();
            builder.Property(reservation => reservation.ConfirmedAtUtc);
            builder.Property(reservation => reservation.ExpiredAtUtc);
            builder.Property(reservation => reservation.CancelledAtUtc);
            builder.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(reservation => reservation.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(reservation => reservation.BuyerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Cart>()
                .WithMany()
                .HasForeignKey(reservation => reservation.CartId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryMovement>(builder =>
        {
            builder.ToTable("inventory_movements");
            builder.HasKey(movement => movement.Id);
            builder.HasIndex(movement => new { movement.SellerId, movement.OccurredAtUtc });
            builder.HasIndex(movement => movement.ProductId);
            builder.HasIndex(movement => movement.ProductVariantId);
            builder.HasIndex(movement => movement.MovementType);
            builder.HasIndex(movement => movement.BatchReference);
            builder.HasIndex(movement => movement.CartId);
            builder.HasIndex(movement => movement.OrderId);
            builder.HasIndex(movement => movement.ReservationId);
            builder.HasIndex(movement => movement.PaymentId);
            builder.HasIndex(movement => movement.ReturnRequestId);
            builder.HasIndex(movement => movement.RefundId);
            builder.Property(movement => movement.MovementType)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(movement => movement.StockQuantityBefore).IsRequired();
            builder.Property(movement => movement.StockQuantityAfter).IsRequired();
            builder.Property(movement => movement.ReservedQuantityBefore).IsRequired();
            builder.Property(movement => movement.ReservedQuantityAfter).IsRequired();
            builder.Property(movement => movement.QuantityDelta).IsRequired();
            builder.Property(movement => movement.StatusBefore)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(movement => movement.StatusAfter)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(movement => movement.Source).HasMaxLength(InventoryMovement.SourceMaxLength).IsRequired();
            builder.Property(movement => movement.Reason).HasMaxLength(InventoryMovement.ReasonMaxLength).IsRequired();
            builder.Property(movement => movement.BatchReference).HasMaxLength(InventoryMovement.BatchReferenceMaxLength);
            builder.Property(movement => movement.OccurredAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(movement => movement.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(movement => movement.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(movement => movement.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnRestockDecision>(builder =>
        {
            builder.ToTable("return_restock_decisions");
            builder.HasKey(decision => decision.Id);
            builder.HasIndex(decision => decision.SellerId);
            builder.HasIndex(decision => decision.ReturnRequestId);
            builder.HasIndex(decision => decision.ReturnItemId).IsUnique();
            builder.HasIndex(decision => decision.ProductVariantId);
            builder.Property(decision => decision.QuantityRestocked).IsRequired();
            builder.Property(decision => decision.Condition)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(decision => decision.Reason).HasMaxLength(ReturnRestockDecision.ReasonMaxLength).IsRequired();
            builder.Property(decision => decision.CreatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(decision => decision.SellerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ReturnRequest>()
                .WithMany()
                .HasForeignKey(decision => decision.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ReturnItem>()
                .WithMany()
                .HasForeignKey(decision => decision.ReturnItemId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(decision => decision.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(decision => decision.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("orders");
            builder.HasKey(order => order.Id);
            builder.HasIndex(order => order.BuyerId);
            builder.HasIndex(order => order.SellerId);
            builder.HasIndex(order => order.CartId);
            builder.HasIndex(order => order.Status);
            builder.HasIndex(order => order.CreatedAtUtc);
            builder.HasIndex(order => new { order.CartId, order.BuyerId, order.Status });
            builder.Property(order => order.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(order => order.ShippingAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(order => order.PlatformFeeAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(order => order.DiscountAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(order => order.DeliveryMethodName).HasMaxLength(SellerDeliveryMethod.NameMaxLength);
            builder.Property(order => order.DeliveryMethodType).HasMaxLength(40);
            builder.Property(order => order.DeliveryRecipientName).HasMaxLength(BuyerDeliveryAddress.RecipientNameMaxLength);
            builder.Property(order => order.DeliveryPhoneNumber).HasMaxLength(BuyerDeliveryAddress.PhoneNumberMaxLength);
            builder.Property(order => order.DeliveryAddressLine1).HasMaxLength(BuyerDeliveryAddress.AddressLineMaxLength);
            builder.Property(order => order.DeliveryAddressLine2).HasMaxLength(BuyerDeliveryAddress.AddressLineMaxLength);
            builder.Property(order => order.DeliverySuburb).HasMaxLength(BuyerDeliveryAddress.SuburbMaxLength);
            builder.Property(order => order.DeliveryCity).HasMaxLength(BuyerDeliveryAddress.CityMaxLength);
            builder.Property(order => order.DeliveryProvince).HasMaxLength(BuyerDeliveryAddress.ProvinceMaxLength);
            builder.Property(order => order.DeliveryPostalCode).HasMaxLength(BuyerDeliveryAddress.PostalCodeMaxLength);
            builder.Property(order => order.DeliveryCountryCode).HasMaxLength(BuyerDeliveryAddress.CountryCodeLength);
            builder.Property(order => order.DeliveryInstructions).HasMaxLength(BuyerDeliveryAddress.DeliveryInstructionsMaxLength);
            builder.Property(order => order.DeliveryVerificationStatus)
                .HasConversion<string>()
                .HasMaxLength(40)
                .HasDefaultValue(AddressVerificationStatus.Unverified)
                .IsRequired();
            builder.Property(order => order.DeliveryVerificationProvider).HasMaxLength(BuyerDeliveryAddress.VerificationProviderMaxLength);
            builder.Property(order => order.DeliveryVerificationWarningsJson).HasColumnType("jsonb");
            builder.HasIndex(order => order.PickupPointId);
            builder.Property(order => order.PickupPointProviderName).HasMaxLength(PickupPoint.ProviderNameMaxLength);
            builder.Property(order => order.PickupPointCode).HasMaxLength(PickupPoint.CodeMaxLength);
            builder.Property(order => order.PickupPointName).HasMaxLength(PickupPoint.NameMaxLength);
            builder.Property(order => order.PickupPointAddressLine1).HasMaxLength(PickupPoint.AddressLineMaxLength);
            builder.Property(order => order.PickupPointAddressLine2).HasMaxLength(PickupPoint.AddressLineMaxLength);
            builder.Property(order => order.PickupPointSuburb).HasMaxLength(PickupPoint.SuburbMaxLength);
            builder.Property(order => order.PickupPointCity).HasMaxLength(PickupPoint.CityMaxLength);
            builder.Property(order => order.PickupPointProvince).HasMaxLength(PickupPoint.ProvinceMaxLength);
            builder.Property(order => order.PickupPointPostalCode).HasMaxLength(PickupPoint.PostalCodeMaxLength);
            builder.Property(order => order.PickupPointCountryCode).HasMaxLength(PickupPoint.CountryCodeLength);
            builder.Property(order => order.PickupPointLatitude).HasPrecision(9, 6);
            builder.Property(order => order.PickupPointLongitude).HasPrecision(9, 6);
            builder.Property(order => order.PickupPointOpeningHours).HasMaxLength(PickupPoint.OpeningHoursMaxLength);
            builder.Property(order => order.SellerPolicyReturnPolicy).HasMaxLength(SellerStorePolicy.ReturnPolicyMaxLength);
            builder.Property(order => order.SellerPolicyExchangePolicy).HasMaxLength(SellerStorePolicy.ExchangePolicyMaxLength);
            builder.Property(order => order.SellerPolicyFulfilmentPolicy).HasMaxLength(SellerStorePolicy.FulfilmentPolicyMaxLength);
            builder.Property(order => order.SellerPolicySupportPolicy).HasMaxLength(SellerStorePolicy.SupportPolicyMaxLength);
            builder.Property(order => order.SellerPolicyCareInstructions).HasMaxLength(SellerStorePolicy.CareInstructionsMaxLength);
            builder.Property(order => order.SellerPolicyProductDisclaimer).HasMaxLength(SellerStorePolicy.ProductDisclaimerMaxLength);
            builder.Property(order => order.CreatedAtUtc).IsRequired();
            builder.Property(order => order.UpdatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(order => order.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(order => order.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Cart>()
                .WithMany()
                .HasForeignKey(order => order.CartId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(order => order.Items)
                .WithOne()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.StatusHistory)
                .WithOne()
                .HasForeignKey(history => history.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.Shipments)
                .WithOne()
                .HasForeignKey(shipment => shipment.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(Order.Items))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(Order.StatusHistory))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(Order.Shipments))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.ToTable("order_items");
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => item.OrderId);
            builder.HasIndex(item => item.ProductVariantId);
            builder.Property(item => item.ProductTitle).HasMaxLength(200);
            builder.Property(item => item.Sku).HasMaxLength(120).IsRequired();
            builder.Property(item => item.Size).HasMaxLength(64).IsRequired();
            builder.Property(item => item.Colour).HasMaxLength(64).IsRequired();
            builder.Property(item => item.UnitPrice).HasPrecision(18, 2).IsRequired();
            builder.Property(item => item.Quantity).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(item => item.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderStatusHistory>(builder =>
        {
            builder.ToTable("order_status_history");
            builder.HasKey(history => history.Id);
            builder.HasIndex(history => history.OrderId);
            builder.HasIndex(history => history.ChangedAtUtc);
            builder.Property(history => history.PreviousStatus)
                .HasConversion<string>()
                .HasMaxLength(64);
            builder.Property(history => history.NewStatus)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(history => history.ChangedAtUtc).IsRequired();
            builder.Property(history => history.Reason).HasMaxLength(1000);
        });

        modelBuilder.Entity<Shipment>(builder =>
        {
            builder.ToTable("shipments");
            builder.HasKey(shipment => shipment.Id);
            builder.HasIndex(shipment => shipment.OrderId);
            builder.HasIndex(shipment => shipment.SellerId);
            builder.HasIndex(shipment => shipment.BuyerId);
            builder.HasIndex(shipment => shipment.Status);
            builder.HasIndex(shipment => shipment.TrackingNumber);
            builder.HasIndex(shipment => shipment.ProviderShipmentReference);
            builder.HasIndex(shipment => shipment.CarrierBookingStatus);
            builder.HasIndex(shipment => shipment.ProviderStatus);
            builder.HasIndex(shipment => shipment.ProviderLastSyncedAtUtc);
            builder.HasIndex(shipment => shipment.CreatedAtUtc);
            builder.Property(shipment => shipment.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(shipment => shipment.CarrierName).HasMaxLength(120);
            builder.Property(shipment => shipment.TrackingNumber).HasMaxLength(160);
            builder.Property(shipment => shipment.TrackingUrl).HasMaxLength(500);
            builder.Property(shipment => shipment.CarrierProviderName).HasMaxLength(Shipment.CarrierProviderNameMaxLength);
            builder.Property(shipment => shipment.CarrierServiceCode).HasMaxLength(Shipment.CarrierServiceCodeMaxLength);
            builder.Property(shipment => shipment.ProviderShipmentReference).HasMaxLength(Shipment.ProviderShipmentReferenceMaxLength);
            builder.Property(shipment => shipment.CarrierBookingStatus)
                .HasConversion<string>()
                .HasMaxLength(64);
            builder.Property(shipment => shipment.ProviderStatus).HasMaxLength(Shipment.ProviderStatusMaxLength);
            builder.Property(shipment => shipment.ProviderLabelUrl).HasMaxLength(Shipment.ProviderLabelUrlMaxLength);
            builder.Property(shipment => shipment.ProviderError).HasMaxLength(Shipment.ProviderErrorMaxLength);
            builder.Property(shipment => shipment.PackageWeightKg).HasPrecision(10, 3);
            builder.Property(shipment => shipment.PackageLengthCm).HasPrecision(10, 2);
            builder.Property(shipment => shipment.PackageWidthCm).HasPrecision(10, 2);
            builder.Property(shipment => shipment.PackageHeightCm).HasPrecision(10, 2);
            builder.Property(shipment => shipment.CreatedAtUtc).IsRequired();
            builder.Property(shipment => shipment.UpdatedAtUtc).IsRequired();
            builder.HasOne<Order>()
                .WithMany(order => order.Shipments)
                .HasForeignKey(shipment => shipment.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(shipment => shipment.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(shipment => shipment.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(shipment => shipment.Events)
                .WithOne()
                .HasForeignKey(shipmentEvent => shipmentEvent.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(Shipment.Events))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ShipmentEvent>(builder =>
        {
            builder.ToTable("shipment_events");
            builder.HasKey(shipmentEvent => shipmentEvent.Id);
            builder.HasIndex(shipmentEvent => shipmentEvent.ShipmentId);
            builder.HasIndex(shipmentEvent => shipmentEvent.Status);
            builder.HasIndex(shipmentEvent => shipmentEvent.EventType);
            builder.HasIndex(shipmentEvent => shipmentEvent.OccurredAtUtc);
            builder.Property(shipmentEvent => shipmentEvent.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(shipmentEvent => shipmentEvent.EventType).HasMaxLength(120).IsRequired();
            builder.Property(shipmentEvent => shipmentEvent.Message).HasMaxLength(1000);
            builder.Property(shipmentEvent => shipmentEvent.CarrierName).HasMaxLength(120);
            builder.Property(shipmentEvent => shipmentEvent.TrackingNumber).HasMaxLength(160);
            builder.Property(shipmentEvent => shipmentEvent.OccurredAtUtc).IsRequired();
        });

        modelBuilder.Entity<ReturnRequest>(builder =>
        {
            builder.ToTable("return_requests");
            builder.HasKey(returnRequest => returnRequest.Id);
            builder.HasIndex(returnRequest => returnRequest.OrderId);
            builder.HasIndex(returnRequest => returnRequest.BuyerId);
            builder.HasIndex(returnRequest => returnRequest.SellerId);
            builder.HasIndex(returnRequest => returnRequest.Status);
            builder.HasIndex(returnRequest => returnRequest.Reason);
            builder.HasIndex(returnRequest => returnRequest.RequestedAtUtc);
            builder.Property(returnRequest => returnRequest.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(returnRequest => returnRequest.Reason)
                .HasConversion<string>()
                .HasMaxLength(80)
                .IsRequired();
            builder.Property(returnRequest => returnRequest.Details).HasMaxLength(2000);
            builder.Property(returnRequest => returnRequest.SellerResponseReason).HasMaxLength(2000);
            builder.Property(returnRequest => returnRequest.DisputeReason).HasMaxLength(2000);
            builder.Property(returnRequest => returnRequest.RequestedAtUtc).IsRequired();
            builder.Property(returnRequest => returnRequest.CreatedAtUtc).IsRequired();
            builder.Property(returnRequest => returnRequest.UpdatedAtUtc).IsRequired();
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(returnRequest => returnRequest.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(returnRequest => returnRequest.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(returnRequest => returnRequest.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(returnRequest => returnRequest.Items)
                .WithOne()
                .HasForeignKey(item => item.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(returnRequest => returnRequest.Messages)
                .WithOne()
                .HasForeignKey(message => message.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(ReturnRequest.Items))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(ReturnRequest.Messages))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ReturnItem>(builder =>
        {
            builder.ToTable("return_items");
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => item.ReturnRequestId);
            builder.HasIndex(item => item.OrderItemId);
            builder.HasIndex(item => item.ProductId);
            builder.HasIndex(item => item.ProductVariantId);
            builder.Property(item => item.Reason)
                .HasConversion<string>()
                .HasMaxLength(80)
                .IsRequired();
            builder.Property(item => item.Note).HasMaxLength(1000);
            builder.HasOne<ReturnRequest>()
                .WithMany(returnRequest => returnRequest.Items)
                .HasForeignKey(item => item.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<OrderItem>()
                .WithMany()
                .HasForeignKey(item => item.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ProductVariant>()
                .WithMany()
                .HasForeignKey(item => item.ProductVariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReturnMessage>(builder =>
        {
            builder.ToTable("return_messages");
            builder.HasKey(message => message.Id);
            builder.HasIndex(message => message.ReturnRequestId);
            builder.HasIndex(message => message.SenderUserId);
            builder.HasIndex(message => message.CreatedAtUtc);
            builder.Property(message => message.SenderRole).HasMaxLength(64).IsRequired();
            builder.Property(message => message.Message).HasMaxLength(2000).IsRequired();
            builder.Property(message => message.CreatedAtUtc).IsRequired();
            builder.HasOne<ReturnRequest>()
                .WithMany(returnRequest => returnRequest.Messages)
                .HasForeignKey(message => message.ReturnRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Refund>(builder =>
        {
            builder.ToTable("refunds");
            builder.HasKey(refund => refund.Id);
            builder.HasIndex(refund => refund.OrderId);
            builder.HasIndex(refund => refund.PaymentId);
            builder.HasIndex(refund => refund.BuyerId);
            builder.HasIndex(refund => refund.SellerId);
            builder.HasIndex(refund => refund.ReturnRequestId);
            builder.HasIndex(refund => refund.Status);
            builder.HasIndex(refund => refund.RequestedAtUtc);
            builder.Property(refund => refund.ConcurrencyVersion)
                .IsConcurrencyToken()
                .IsRequired();
            builder.Property(refund => refund.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(refund => refund.Currency).HasMaxLength(3).IsRequired();
            builder.Property(refund => refund.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(refund => refund.Reason).HasMaxLength(2000).IsRequired();
            builder.Property(refund => refund.RequestedByRole).HasMaxLength(64).IsRequired();
            builder.Property(refund => refund.ApprovalReason).HasMaxLength(2000);
            builder.Property(refund => refund.ProviderRefundReference).HasMaxLength(256);
            builder.Property(refund => refund.FailureReason).HasMaxLength(2000);
            builder.Property(refund => refund.RequestedAtUtc).IsRequired();
            builder.Property(refund => refund.CreatedAtUtc).IsRequired();
            builder.Property(refund => refund.UpdatedAtUtc).IsRequired();
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(refund => refund.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(refund => refund.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(refund => refund.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(refund => refund.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ReturnRequest>()
                .WithMany()
                .HasForeignKey(refund => refund.ReturnRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(refund => refund.Events)
                .WithOne()
                .HasForeignKey(refundEvent => refundEvent.RefundId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(Refund.Events))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RefundEvent>(builder =>
        {
            builder.ToTable("refund_events");
            builder.HasKey(refundEvent => refundEvent.Id);
            builder.HasIndex(refundEvent => refundEvent.RefundId);
            builder.HasIndex(refundEvent => refundEvent.Status);
            builder.HasIndex(refundEvent => refundEvent.EventType);
            builder.HasIndex(refundEvent => refundEvent.CreatedAtUtc);
            builder.Property(refundEvent => refundEvent.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(refundEvent => refundEvent.EventType).HasMaxLength(120).IsRequired();
            builder.Property(refundEvent => refundEvent.Message).HasMaxLength(2000).IsRequired();
            builder.Property(refundEvent => refundEvent.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Dispute>(builder =>
        {
            builder.ToTable("disputes");
            builder.HasKey(dispute => dispute.Id);
            builder.HasIndex(dispute => dispute.OrderId);
            builder.HasIndex(dispute => dispute.ReturnRequestId);
            builder.HasIndex(dispute => dispute.BuyerId);
            builder.HasIndex(dispute => dispute.SellerId);
            builder.HasIndex(dispute => dispute.Status);
            builder.HasIndex(dispute => dispute.OpenedAtUtc);
            builder.Property(dispute => dispute.Status)
                .HasConversion<string>()
                .HasMaxLength(80)
                .IsRequired();
            builder.Property(dispute => dispute.Reason).HasMaxLength(2000).IsRequired();
            builder.Property(dispute => dispute.ResolutionReason).HasMaxLength(2000);
            builder.Property(dispute => dispute.OpenedAtUtc).IsRequired();
            builder.Property(dispute => dispute.CreatedAtUtc).IsRequired();
            builder.Property(dispute => dispute.UpdatedAtUtc).IsRequired();
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(dispute => dispute.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ReturnRequest>()
                .WithMany()
                .HasForeignKey(dispute => dispute.ReturnRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(dispute => dispute.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(dispute => dispute.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(dispute => dispute.Messages)
                .WithOne()
                .HasForeignKey(message => message.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(dispute => dispute.Evidence)
                .WithOne()
                .HasForeignKey(evidence => evidence.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(Dispute.Messages))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
            builder.Metadata.FindNavigation(nameof(Dispute.Evidence))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<DisputeMessage>(builder =>
        {
            builder.ToTable("dispute_messages");
            builder.HasKey(message => message.Id);
            builder.HasIndex(message => message.DisputeId);
            builder.HasIndex(message => message.SenderUserId);
            builder.HasIndex(message => message.CreatedAtUtc);
            builder.Property(message => message.SenderRole).HasMaxLength(64).IsRequired();
            builder.Property(message => message.Message).HasMaxLength(2000).IsRequired();
            builder.Property(message => message.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<DisputeEvidence>(builder =>
        {
            builder.ToTable("dispute_evidence");
            builder.HasKey(evidence => evidence.Id);
            builder.HasIndex(evidence => evidence.DisputeId);
            builder.HasIndex(evidence => evidence.SubmittedByUserId);
            builder.HasIndex(evidence => evidence.EvidenceType);
            builder.HasIndex(evidence => evidence.CreatedAtUtc);
            builder.Property(evidence => evidence.SubmittedByRole).HasMaxLength(64).IsRequired();
            builder.Property(evidence => evidence.EvidenceType).HasMaxLength(80).IsRequired();
            builder.Property(evidence => evidence.StorageReference).HasMaxLength(500).IsRequired();
            builder.Property(evidence => evidence.Description).HasMaxLength(1000);
            builder.Property(evidence => evidence.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<SupportTicket>(builder =>
        {
            builder.ToTable("support_tickets");
            builder.HasKey(ticket => ticket.Id);
            builder.HasIndex(ticket => ticket.CreatedByUserId);
            builder.HasIndex(ticket => ticket.BuyerId);
            builder.HasIndex(ticket => ticket.SellerId);
            builder.HasIndex(ticket => ticket.Category);
            builder.HasIndex(ticket => ticket.Status);
            builder.HasIndex(ticket => ticket.LinkedOrderId);
            builder.HasIndex(ticket => ticket.LinkedProductId);
            builder.HasIndex(ticket => ticket.LinkedSellerId);
            builder.HasIndex(ticket => ticket.LinkedPaymentId);
            builder.HasIndex(ticket => ticket.OpenedAtUtc);
            builder.Property(ticket => ticket.CreatedByRole).HasMaxLength(64).IsRequired();
            builder.Property(ticket => ticket.Category)
                .HasConversion<string>()
                .HasMaxLength(80)
                .IsRequired();
            builder.Property(ticket => ticket.Status)
                .HasConversion<string>()
                .HasMaxLength(80)
                .IsRequired();
            builder.Property(ticket => ticket.Priority)
                .HasConversion<string>()
                .HasMaxLength(80)
                .HasDefaultValue(SupportTicketPriority.Normal)
                .IsRequired();
            builder.Property(ticket => ticket.EscalationReason).HasMaxLength(1000);
            builder.Property(ticket => ticket.Subject).HasMaxLength(200).IsRequired();
            builder.Property(ticket => ticket.Description).HasMaxLength(4000).IsRequired();
            builder.Property(ticket => ticket.OpenedAtUtc).IsRequired();
            builder.Property(ticket => ticket.CreatedAtUtc).IsRequired();
            builder.Property(ticket => ticket.UpdatedAtUtc).IsRequired();
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(ticket => ticket.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(ticket => ticket.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(ticket => ticket.LinkedOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(ticket => ticket.LinkedProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(ticket => ticket.LinkedSellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(ticket => ticket.LinkedPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(ticket => ticket.Messages)
                .WithOne()
                .HasForeignKey(message => message.SupportTicketId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(SupportTicket.Messages))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SupportMessage>(builder =>
        {
            builder.ToTable("support_messages");
            builder.HasKey(message => message.Id);
            builder.HasIndex(message => message.SupportTicketId);
            builder.HasIndex(message => message.SenderUserId);
            builder.HasIndex(message => message.IsInternal);
            builder.HasIndex(message => message.CreatedAtUtc);
            builder.Property(message => message.SenderRole).HasMaxLength(64).IsRequired();
            builder.Property(message => message.Message).HasMaxLength(4000).IsRequired();
            builder.Property(message => message.IsInternal).IsRequired();
            builder.Property(message => message.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<AdCampaign>(builder =>
        {
            builder.ToTable("ad_campaigns");
            builder.HasKey(campaign => campaign.Id);
            builder.HasIndex(campaign => campaign.SellerId);
            builder.HasIndex(campaign => campaign.Status);
            builder.HasIndex(campaign => campaign.CampaignType);
            builder.HasIndex(campaign => new { campaign.StartsAtUtc, campaign.EndsAtUtc });
            builder.Property(campaign => campaign.Name).HasMaxLength(160).IsRequired();
            builder.Property(campaign => campaign.CampaignType)
                .HasConversion<string>()
                .HasMaxLength(80)
                .IsRequired();
            builder.Property(campaign => campaign.Status)
                .HasConversion<string>()
                .HasMaxLength(80)
                .IsRequired();
            builder.Property(campaign => campaign.RejectionReason).HasMaxLength(1000);
            builder.Property(campaign => campaign.CreatedAtUtc).IsRequired();
            builder.Property(campaign => campaign.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(campaign => campaign.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(campaign => campaign.Products)
                .WithOne()
                .HasForeignKey(product => product.AdCampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(AdCampaign.Products))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<AdCampaignProduct>(builder =>
        {
            builder.ToTable("ad_campaign_products");
            builder.HasKey(product => product.Id);
            builder.HasIndex(product => new { product.AdCampaignId, product.ProductId }).IsUnique();
            builder.HasIndex(product => product.ProductId);
            builder.Property(product => product.CreatedAtUtc).IsRequired();
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(product => product.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdBudget>(builder =>
        {
            builder.ToTable("ad_budgets");
            builder.HasKey(budget => budget.Id);
            builder.HasIndex(budget => budget.AdCampaignId).IsUnique();
            builder.Property(budget => budget.Currency).HasMaxLength(3).IsRequired();
            builder.Property(budget => budget.DailyBudget).HasPrecision(18, 2).IsRequired();
            builder.Property(budget => budget.TotalBudget).HasPrecision(18, 2).IsRequired();
            builder.Property(budget => budget.MaxCostPerClick).HasPrecision(18, 2).IsRequired();
            builder.Property(budget => budget.SpentAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(budget => budget.CreatedAtUtc).IsRequired();
            builder.Property(budget => budget.UpdatedAtUtc).IsRequired();
            builder.HasOne<AdCampaign>()
                .WithOne()
                .HasForeignKey<AdBudget>(budget => budget.AdCampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdImpression>(builder =>
        {
            builder.ToTable("ad_impressions");
            builder.HasKey(impression => impression.Id);
            builder.HasIndex(impression => impression.AdCampaignId);
            builder.HasIndex(impression => impression.ProductId);
            builder.HasIndex(impression => impression.OccurredAtUtc);
            builder.HasIndex(impression => new { impression.AdCampaignId, impression.AnonymousVisitorId, impression.OccurredAtUtc });
            builder.Property(impression => impression.Placement).HasMaxLength(120).IsRequired();
            builder.Property(impression => impression.AnonymousVisitorId).HasMaxLength(128);
            builder.Property(impression => impression.OccurredAtUtc).IsRequired();
            builder.HasOne<AdCampaign>()
                .WithMany()
                .HasForeignKey(impression => impression.AdCampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(impression => impression.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdClick>(builder =>
        {
            builder.ToTable("ad_clicks");
            builder.HasKey(click => click.Id);
            builder.HasIndex(click => click.AdCampaignId);
            builder.HasIndex(click => click.ProductId);
            builder.HasIndex(click => click.BuyerId);
            builder.HasIndex(click => click.OccurredAtUtc);
            builder.Property(click => click.AnonymousVisitorId).HasMaxLength(128);
            builder.Property(click => click.OccurredAtUtc).IsRequired();
            builder.HasOne<AdCampaign>()
                .WithMany()
                .HasForeignKey(click => click.AdCampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Product>()
                .WithMany()
                .HasForeignKey(click => click.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(click => click.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdConversion>(builder =>
        {
            builder.ToTable("ad_conversions");
            builder.HasKey(conversion => conversion.Id);
            builder.HasIndex(conversion => conversion.AdCampaignId);
            builder.HasIndex(conversion => conversion.AdClickId).IsUnique();
            builder.HasIndex(conversion => conversion.OrderId);
            builder.HasIndex(conversion => conversion.OccurredAtUtc);
            builder.Property(conversion => conversion.RevenueAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(conversion => conversion.Currency).HasMaxLength(3).IsRequired();
            builder.Property(conversion => conversion.OccurredAtUtc).IsRequired();
            builder.HasOne<AdCampaign>()
                .WithMany()
                .HasForeignKey(conversion => conversion.AdCampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<AdClick>()
                .WithMany()
                .HasForeignKey(conversion => conversion.AdClickId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(conversion => conversion.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdCharge>(builder =>
        {
            builder.ToTable("ad_charges");
            builder.HasKey(charge => charge.Id);
            builder.HasIndex(charge => charge.AdCampaignId);
            builder.HasIndex(charge => charge.AdClickId);
            builder.HasIndex(charge => charge.ChargedAtUtc);
            builder.Property(charge => charge.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(charge => charge.Currency).HasMaxLength(3).IsRequired();
            builder.Property(charge => charge.Reason).HasMaxLength(240).IsRequired();
            builder.Property(charge => charge.ChargedAtUtc).IsRequired();
            builder.HasOne<AdCampaign>()
                .WithMany()
                .HasForeignKey(charge => charge.AdCampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<AdClick>()
                .WithMany()
                .HasForeignKey(charge => charge.AdClickId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SellerAdCredit>(builder =>
        {
            builder.ToTable("seller_ad_credits");
            builder.HasKey(credit => credit.Id);
            builder.HasIndex(credit => new { credit.SellerId, credit.Currency }).IsUnique();
            builder.Property(credit => credit.Currency).HasMaxLength(3).IsRequired();
            builder.Property(credit => credit.Balance).HasPrecision(18, 2).IsRequired();
            builder.Property(credit => credit.CreatedAtUtc).IsRequired();
            builder.Property(credit => credit.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(credit => credit.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(builder =>
        {
            builder.ToTable("payments");
            builder.HasKey(payment => payment.Id);
            builder.HasIndex(payment => payment.OrderId)
                .IsUnique()
                .HasFilter("\"Status\" NOT IN ('Failed', 'Cancelled')");
            builder.HasIndex(payment => payment.BuyerId);
            builder.HasIndex(payment => payment.ProviderReference);
            builder.HasIndex(payment => payment.Status);
            builder.HasIndex(payment => payment.CreatedAtUtc);
            builder.Property(payment => payment.Provider).HasMaxLength(64).IsRequired();
            builder.Property(payment => payment.ProviderReference).HasMaxLength(256);
            builder.Property(payment => payment.CheckoutUrl)
                .HasColumnName("checkout_url")
                .HasMaxLength(1000);
            builder.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            builder.Property(payment => payment.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(payment => payment.CreatedAtUtc).IsRequired();
            builder.Property(payment => payment.UpdatedAtUtc).IsRequired();
            builder.Property(payment => payment.PaidAtUtc);
            builder.Property(payment => payment.FailedAtUtc);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(payment => payment.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(payment => payment.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentEvent>(builder =>
        {
            builder.ToTable("payment_events");
            builder.HasKey(paymentEvent => paymentEvent.Id);
            builder.HasIndex(paymentEvent => paymentEvent.PaymentId);
            builder.HasIndex(paymentEvent => new { paymentEvent.Provider, paymentEvent.ProviderEventId }).IsUnique();
            builder.HasIndex(paymentEvent => paymentEvent.ReceivedAtUtc);
            builder.Property(paymentEvent => paymentEvent.Provider).HasMaxLength(64).IsRequired();
            builder.Property(paymentEvent => paymentEvent.ProviderEventId).HasMaxLength(256).IsRequired();
            builder.Property(paymentEvent => paymentEvent.EventType).HasMaxLength(128).IsRequired();
            builder.Property(paymentEvent => paymentEvent.RawPayloadJson)
                .HasColumnName("raw_payload_json")
                .HasColumnType("jsonb")
                .IsRequired();
            builder.Property(paymentEvent => paymentEvent.RawPayloadRedactedAtUtc)
                .HasColumnName("raw_payload_redacted_at_utc");
            builder.Property(paymentEvent => paymentEvent.ReceivedAtUtc).IsRequired();
            builder.Property(paymentEvent => paymentEvent.ProcessedAtUtc);
            builder.Property(paymentEvent => paymentEvent.ProcessingStatus)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(paymentEvent => paymentEvent.ErrorMessage).HasMaxLength(2000);
            builder.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(paymentEvent => paymentEvent.PaymentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PaymentReconciliationReview>(builder =>
        {
            builder.ToTable("payment_reconciliation_reviews");
            builder.HasKey(review => review.Id);
            builder.HasIndex(review => new { review.PaymentId, review.ReviewedAtUtc });
            builder.HasIndex(review => new { review.Provider, review.ProviderReference });
            builder.HasIndex(review => review.Outcome);
            builder.HasIndex(review => review.NextReviewAfterUtc);
            builder.Property(review => review.Provider).HasMaxLength(64).IsRequired();
            builder.Property(review => review.ProviderReference).HasMaxLength(256);
            builder.Property(review => review.ObservedProviderStatus).HasMaxLength(128).IsRequired();
            builder.Property(review => review.ObservedAmount).HasPrecision(18, 2);
            builder.Property(review => review.ObservedCurrency).HasMaxLength(3);
            builder.Property(review => review.Outcome)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(review => review.Reason).HasMaxLength(1000).IsRequired();
            builder.Property(review => review.ReviewedAtUtc).IsRequired();
            builder.Property(review => review.NextReviewAfterUtc);
            builder.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(review => review.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LedgerEntry>(builder =>
        {
            builder.ToTable("ledger_entries");
            builder.HasKey(entry => entry.Id);
            builder.HasIndex(entry => entry.OrderId);
            builder.HasIndex(entry => entry.PaymentId);
            builder.HasIndex(entry => entry.SellerId);
            builder.HasIndex(entry => entry.BuyerId);
            builder.HasIndex(entry => entry.CreatedAtUtc);
            builder.HasIndex(entry => new { entry.PaymentId, entry.Type });
            builder.Property(entry => entry.Type)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(entry => entry.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(entry => entry.Currency).HasMaxLength(3).IsRequired();
            builder.Property(entry => entry.Direction)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            builder.Property(entry => entry.Description).HasMaxLength(500).IsRequired();
            builder.Property(entry => entry.CreatedAtUtc).IsRequired();
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(entry => entry.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OrderItem>()
                .WithMany()
                .HasForeignKey(entry => entry.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(entry => entry.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<BuyerProfile>()
                .WithMany()
                .HasForeignKey(entry => entry.BuyerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(entry => entry.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SellerBalance>(builder =>
        {
            builder.ToTable("seller_balances");
            builder.HasKey(balance => balance.Id);
            builder.HasIndex(balance => new { balance.SellerId, balance.Currency }).IsUnique();
            builder.Property(balance => balance.Currency).HasMaxLength(3).IsRequired();
            builder.Property(balance => balance.PendingBalance).HasPrecision(18, 2).IsRequired();
            builder.Property(balance => balance.AvailableBalance).HasPrecision(18, 2).IsRequired();
            builder.Property(balance => balance.HeldBalance).HasPrecision(18, 2).IsRequired();
            builder.Property(balance => balance.CreatedAtUtc).IsRequired();
            builder.Property(balance => balance.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(balance => balance.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CommissionRule>(builder =>
        {
            builder.ToTable("commission_rules");
            builder.HasKey(rule => rule.Id);
            builder.HasIndex(rule => rule.IsActive);
            builder.Property(rule => rule.Name).HasMaxLength(128).IsRequired();
            builder.Property(rule => rule.PlatformCommissionRatePercent).HasPrecision(5, 2).IsRequired();
            builder.Property(rule => rule.PaymentProviderFeeRatePercent).HasPrecision(5, 2).IsRequired();
            builder.Property(rule => rule.PaymentProviderFixedFee).HasPrecision(18, 2).IsRequired();
            builder.Property(rule => rule.Currency).HasMaxLength(3).IsRequired();
            builder.Property(rule => rule.IsActive).IsRequired();
            builder.Property(rule => rule.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<SellerPayout>(builder =>
        {
            builder.ToTable("seller_payouts");
            builder.HasKey(payout => payout.Id);
            builder.HasIndex(payout => payout.SellerId);
            builder.HasIndex(payout => payout.Status);
            builder.HasIndex(payout => payout.CreatedAtUtc);
            builder.Property(payout => payout.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(payout => payout.Currency).HasMaxLength(3).IsRequired();
            builder.Property(payout => payout.Status)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(payout => payout.HeldFromStatus)
                .HasConversion<string>()
                .HasMaxLength(64);
            builder.Property(payout => payout.HeldByUserId).HasMaxLength(64);
            builder.Property(payout => payout.HoldReason).HasMaxLength(1000);
            builder.Property(payout => payout.ReleasedByUserId).HasMaxLength(64);
            builder.Property(payout => payout.ReleaseReason).HasMaxLength(1000);
            builder.Property(payout => payout.AvailableByUserId).HasMaxLength(64);
            builder.Property(payout => payout.AvailabilityReason).HasMaxLength(1000);
            builder.Property(payout => payout.ProcessingByUserId).HasMaxLength(64);
            builder.Property(payout => payout.ProcessingReason).HasMaxLength(1000);
            builder.Property(payout => payout.FailureReason).HasMaxLength(1000);
            builder.Property(payout => payout.ProviderName).HasMaxLength(64);
            builder.Property(payout => payout.ProviderPayoutReference).HasMaxLength(256);
            builder.Property(payout => payout.ProviderStatus).HasMaxLength(64);
            builder.Property(payout => payout.ConcurrencyVersion)
                .IsConcurrencyToken()
                .IsRequired();
            builder.Property(payout => payout.CreatedAtUtc).IsRequired();
            builder.Property(payout => payout.UpdatedAtUtc).IsRequired();
            builder.HasOne<SellerProfile>()
                .WithMany()
                .HasForeignKey(payout => payout.SellerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(payout => payout.Items)
                .WithOne()
                .HasForeignKey(item => item.SellerPayoutId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Metadata.FindNavigation(nameof(SellerPayout.Items))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<SellerPayoutItem>(builder =>
        {
            builder.ToTable("seller_payout_items");
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => item.SellerPayoutId);
            builder.HasIndex(item => item.LedgerEntryId).IsUnique();
            builder.HasIndex(item => item.OrderId);
            builder.HasIndex(item => item.PaymentId);
            builder.Property(item => item.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(item => item.AdjustedAmount).HasPrecision(18, 2).IsRequired();
            builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            builder.Property(item => item.CreatedAtUtc).IsRequired();
            builder.HasOne<LedgerEntry>()
                .WithMany()
                .HasForeignKey(item => item.LedgerEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(item => item.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SellerPayoutAdjustment>(builder =>
        {
            builder.ToTable("seller_payout_adjustments");
            builder.HasKey(adjustment => adjustment.Id);
            builder.HasIndex(adjustment => adjustment.SellerPayoutId);
            builder.HasIndex(adjustment => adjustment.SellerPayoutItemId);
            builder.HasIndex(adjustment => adjustment.RefundId);
            builder.HasIndex(adjustment => adjustment.RefundLedgerEntryId);
            builder.HasIndex(adjustment => adjustment.CreatedAtUtc);
            builder.Property(adjustment => adjustment.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(adjustment => adjustment.Currency).HasMaxLength(3).IsRequired();
            builder.Property(adjustment => adjustment.AdjustmentType).HasMaxLength(64).IsRequired();
            builder.Property(adjustment => adjustment.Note).HasMaxLength(1000);
            builder.Property(adjustment => adjustment.CreatedAtUtc).IsRequired();
            builder.HasOne<SellerPayout>()
                .WithMany()
                .HasForeignKey(adjustment => adjustment.SellerPayoutId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<SellerPayoutItem>()
                .WithMany()
                .HasForeignKey(adjustment => adjustment.SellerPayoutItemId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Refund>()
                .WithMany()
                .HasForeignKey(adjustment => adjustment.RefundId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<LedgerEntry>()
                .WithMany()
                .HasForeignKey(adjustment => adjustment.RefundLedgerEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.ToTable("notifications");
            builder.HasKey(notification => notification.Id);
            builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc });
            builder.HasIndex(notification => new { notification.RecipientUserId, notification.ReadAtUtc });
            builder.Property(notification => notification.Type).HasMaxLength(120).IsRequired();
            builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
            builder.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
            builder.Property(notification => notification.RelatedEntityType).HasMaxLength(120);
            builder.Property(notification => notification.IsInAppVisible).IsRequired();
            builder.Property(notification => notification.CreatedAtUtc).IsRequired();
            builder.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(notification => notification.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationEmailDelivery>(builder =>
        {
            builder.ToTable("notification_email_deliveries");
            builder.HasKey(delivery => delivery.Id);
            builder.HasIndex(delivery => delivery.NotificationId);
            builder.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAtUtc });
            builder.Property(delivery => delivery.RecipientEmail).HasMaxLength(320).IsRequired();
            builder.Property(delivery => delivery.Subject).HasMaxLength(200).IsRequired();
            builder.Property(delivery => delivery.Body).HasMaxLength(4000).IsRequired();
            builder.Property(delivery => delivery.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(delivery => delivery.AttemptCount).IsRequired();
            builder.Property(delivery => delivery.FailureReason).HasMaxLength(1000);
            builder.Property(delivery => delivery.CreatedAtUtc).IsRequired();
            builder.Property(delivery => delivery.UpdatedAtUtc).IsRequired();
            builder.HasOne<Notification>(delivery => delivery.Notification)
                .WithMany()
                .HasForeignKey(delivery => delivery.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
