# Mabuntle Codex Development Prompt Pack

**Platform:** Mabuntle  
**Product type:** Transactional ecommerce marketplace  
**Focus categories:** Fashion, clothing, jewellery, accessories, beauty products, lifestyle products  
**Core stack:** Angular, ASP.NET Core / .NET, PostgreSQL, EF Core, OpenAI, pgvector, Typesense or Meilisearch, Cloudinary or Azure Blob Storage, Hangfire or .NET Worker Services  
**Recommended architecture:** Modular monolith with Clean Architecture, Vertical Slice Architecture for features, pragmatic CQRS, EF Core direct usage for simple reads/writes, and specific repositories only for complex aggregates such as Orders, Payments, Payouts, Refunds, Inventory Reservations, and Disputes.

---

## 1. What I think of the name “Mabuntle”

**Mabuntle** is a strong marketplace name. It suggests speed, convenience, and smooth transactions, which works well for a fashion and beauty ecommerce marketplace. The spelling gives it a modern startup feel and makes it more brandable than the plain word “Swiftly.”

The name fits these brand messages:

- Sellers can launch products quickly.
- Buyers can discover products quickly.
- Checkout and delivery should feel smooth.
- The platform feels modern, mobile-first, and energetic.

Potential brand lines:

- **Mabuntle — Fashion moves faster here.**
- **Mabuntle — Shop style, beauty, and accessories from local sellers.**
- **Mabuntle — Discover it. Love it. Buy it.**
- **Mabuntle — The marketplace for fast fashion finds and beauty essentials.**

Things still to verify before committing publicly:

- Trademark availability in your target countries.
- Domain availability.
- Social media handle availability.
- Whether the spelling is easy for customers to remember.
- Whether search engines autocorrect it to “swiftly.”

For product UI, I would use the brand name consistently as **Mabuntle**, not **MABUNTLE**, because the softer title case fits fashion and beauty better.

---

## 2. How to use this prompt pack

Do not ask Codex to build the entire platform in one task. Use these prompts as **small implementation tickets**.

Recommended workflow:

1. Create a Git repository.
2. Add `/docs` and `AGENTS.md` early.
3. Use one prompt per feature or sub-feature.
4. Ask Codex to create a branch or PR for each task.
5. Review every diff manually.
6. Ask Codex to add tests.
7. Run tests locally.
8. Merge only after review.

For each Codex task, aim for:

- One module or feature.
- Clear acceptance criteria.
- Tests required.
- No unrelated changes.
- No secrets in code.
- No guessing for payment, legal, or compliance rules.

---

## 3. Global project rules for Codex

Use this as the foundation for all prompts.

```text
You are helping build Mabuntle, a transactional ecommerce marketplace for fashion, clothing, jewellery, accessories, and beauty products.

Core stack:
- Frontend: Angular, TypeScript, Angular Reactive Forms, Angular Router, Tailwind CSS, Angular Material or PrimeNG.
- Backend: ASP.NET Core Web API, C#, .NET, Entity Framework Core, PostgreSQL.
- Database: PostgreSQL with EF Core migrations.
- AI: OpenAI API called only from the backend.
- Vector search later: pgvector.
- Search later: Typesense or Meilisearch.
- Background jobs: Hangfire or .NET Worker Service.
- Storage: Cloudinary or Azure Blob Storage.

Architecture rules:
- Use a modular monolith.
- Follow Clean Architecture principles.
- Organise backend by modules/features.
- Use Vertical Slice Architecture for commands and queries where useful.
- Use EF Core directly for simple queries and CRUD.
- Do not create a generic repository for every entity.
- Use specific repositories/services only for complex business workflows such as orders, payments, refunds, seller payouts, inventory reservations, returns, and disputes.
- Keep controllers thin.
- Put business rules in domain/application services.
- Add tests for domain and application logic.
- Use explicit status enums for orders, products, sellers, payments, payouts, returns, and disputes.

Security rules:
- Do not expose secrets.
- Do not commit .env files.
- Use .env.example for placeholders.
- Do not call OpenAI, Paystack, Stripe, storage, or email providers directly from Angular.
- Verify authorization on every seller/admin endpoint.
- Sellers must never access another seller’s products, orders, payouts, or campaigns.
- Admin actions must be audited.

Payment rules:
- Do not guess payment provider behavior.
- Use provider abstractions and webhook idempotency.
- Every payment event must be stored.
- Every successful payment must create ledger entries.
- Seller payouts must be based on the internal ledger, not only on provider dashboard data.

AI rules:
- AI suggestions are drafts.
- Sellers must approve or edit AI suggestions before publishing.
- AI must not invent product facts such as material, brand, authenticity, ingredients, expiry date, medical claims, or sizing.
- Beauty claims and counterfeit-risk terms must be flagged for review.
- Store AI usage logs and prompt versions.

Output rules:
- Explain files changed.
- Explain key decisions.
- List tests added or run.
- Mention risks and follow-up work.
- Do not make unrelated changes.
```

---

# Part A — Project Setup Prompts

---

## Prompt 1 — Create the initial monorepo

```text
Context:
We are building Mabuntle, a transactional ecommerce marketplace for fashion, clothing, jewellery, accessories, and beauty products.

Goal:
Create the initial monorepo structure for the project.

Tech stack:
- Frontend: Angular + TypeScript.
- Backend: ASP.NET Core Web API + C#.
- Database: PostgreSQL.
- ORM: Entity Framework Core.
- Architecture: modular monolith with Clean Architecture principles.

Create this structure:
/mabuntle
  /docs
    architecture.md
    feature-roadmap.md
    database-schema.md
    api-contracts.md
    coding-standards.md
  /backend
    /src
      Mabuntle.Api
      Mabuntle.Application
      Mabuntle.Domain
      Mabuntle.Infrastructure
      Mabuntle.Worker
    /tests
      Mabuntle.UnitTests
      Mabuntle.IntegrationTests
  /frontend
    /mabuntle-web
  /database
    /migrations
    /seed
  /scripts
  docker-compose.yml
  README.md
  AGENTS.md
  .gitignore
  .env.example

Requirements:
- Create a .NET solution and add all backend projects.
- Add project references according to Clean Architecture:
  - Api references Application and Infrastructure.
  - Application references Domain.
  - Infrastructure references Application and Domain.
  - Worker references Application and Infrastructure.
  - Tests reference appropriate projects.
- Create an Angular application under /frontend/mabuntle-web.
- Add docker-compose.yml with PostgreSQL.
- Add .env.example with placeholder variables only.
- Add a basic README with setup instructions.
- Add AGENTS.md with project rules.
- Do not implement business features yet.

Acceptance criteria:
- dotnet build passes.
- Angular project builds.
- docker-compose.yml starts PostgreSQL.
- README explains how to run backend, frontend, and database.
- No secrets are committed.

Do not:
- Do not implement auth yet.
- Do not implement products yet.
- Do not add payments yet.
- Do not add AI yet.
```

---

## Prompt 2 — Create AGENTS.md for Codex guidance

```text
Goal:
Create or update AGENTS.md for the Mabuntle repository.

The file should instruct Codex how to work in this repo.

Include:
- Project overview.
- Tech stack.
- Architecture rules.
- Backend project structure.
- Frontend project structure.
- Testing commands.
- Security rules.
- Payment rules.
- AI rules.
- Coding style expectations.
- Output expectations after each task.

Important architecture rules:
- Use modular monolith.
- Follow Clean Architecture principles.
- Use Vertical Slice Architecture for feature implementation where appropriate.
- Do not create a generic repository for every entity.
- Use EF Core directly for simple read/write operations.
- Use specific repositories only when they add value for complex aggregate workflows.
- Keep controllers thin.
- Put business logic in domain/application layer.
- Write tests for business rules.

Important security rules:
- No secrets in source code.
- No .env commits.
- Do not call external providers from Angular.
- Validate seller ownership on seller endpoints.
- Validate admin role on admin endpoints.
- Audit sensitive admin actions.

Acceptance criteria:
- AGENTS.md is clear enough for future Codex tasks.
- It includes dotnet and Angular build/test commands.
- It includes explicit 'do not' rules.
```

---

## Prompt 3 — Add development environment and health checks

```text
Context:
The monorepo exists for Mabuntle.

Goal:
Add the first runnable backend and frontend development environment.

Backend requirements:
- Add ASP.NET Core Web API startup configuration.
- Add /health endpoint returning status, application name, and UTC timestamp.
- Add CORS configuration for local Angular development.
- Add structured logging setup.
- Add appsettings.Development.json with non-secret defaults.

Frontend requirements:
- Add Angular app shell with routes:
  - /
  - /shop
  - /seller
  - /admin
  - /account
- Add placeholder layout components.
- Add header, footer, and placeholder navigation.
- Use the Mabuntle colour palette:
  - Primary: #3A1D32
  - Primary hover: #2A1425
  - Accent: #B76E79
  - Background: #FFF9F4
  - Surface: #FFFFFF
  - Text: #1F1A1C
  - Muted: #6F5E66

Docker requirements:
- PostgreSQL service in docker-compose.
- Optional pgAdmin service only if simple to add.

Acceptance criteria:
- Backend runs and /health works.
- Angular app runs and shows the shell.
- CORS allows local frontend to call local backend.
- No business features implemented yet.
```

---

# Part B — Architecture and Documentation Prompts

---

## Prompt 4 — Write architecture.md

```text
Goal:
Create docs/architecture.md for Mabuntle.

Document the recommended architecture:
- Modular monolith.
- Clean Architecture layers.
- Vertical Slice Architecture for commands/queries.
- Pragmatic CQRS.
- Domain-driven design for complex business areas.
- EF Core direct usage for simple operations.
- Specific repositories only for complex aggregates.
- Background jobs for external/retryable work.
- Outbox pattern for reliable domain events.

Include backend modules:
- Auth
- Buyers
- Sellers
- Catalog
- Inventory
- Cart
- Checkout
- Payments
- Orders
- Shipping
- Returns
- Refunds
- Disputes
- AI
- Search
- Advertising
- Admin
- Notifications
- Reporting

Include a section explaining why not to use a generic repository everywhere:
- EF Core DbContext already provides unit-of-work behavior.
- DbSet already provides repository-like access.
- Generic repositories often hide useful EF Core features.
- Specific repositories are acceptable for complex aggregate workflows.

Include examples:
- Use EF Core directly for product lookup and admin search.
- Use IOrderRepository or IOrderWriteService for order aggregate workflows.
- Use IPaymentLedgerService for ledger entries.
- Use IInventoryReservationService for stock reservation.

Acceptance criteria:
- Clear enough for a developer to follow.
- Includes diagrams in text form where useful.
- Uses Mabuntle terminology.
```

---

## Prompt 5 — Write feature roadmap documentation

```text
Goal:
Create docs/feature-roadmap.md for Mabuntle.

Break the project into phases:

Phase 0: Product rules and policies
Phase 1: Foundation, auth, roles, Angular shell, backend API, database
Phase 2: Seller onboarding and storefronts
Phase 3: Catalog, categories, attributes, products, variants, inventory
Phase 4: AI Fashion Product Listing Assistant
Phase 5: Search, filters, and discovery
Phase 6: Cart, single-seller checkout, inventory reservation, orders
Phase 7: Payments, ledger, seller balances, payouts
Phase 8: Shipping, fulfilment, returns, refunds, disputes
Phase 9: Admin, support, moderation, audit logs
Phase 10: Seller advertising campaigns and promoted listings
Phase 11: Analytics, reporting, KPIs
Phase 12: Buyer AI shopping assistant, visual search, personalization

For each phase include:
- Goal
- Features
- Backend modules
- Frontend pages
- Database tables
- Acceptance criteria
- Risks
- What not to build yet

Acceptance criteria:
- This is a practical roadmap that can guide development tasks.
- Keep the roadmap specific to Mabuntle.
```

---

## Prompt 6 — Write coding standards documentation

```text
Goal:
Create docs/coding-standards.md for Mabuntle.

Include standards for:
- C# naming and structure.
- Angular naming and structure.
- DTO naming.
- API route naming.
- Status enum naming.
- Validation.
- Error handling.
- Logging.
- Testing.
- Database migrations.
- External provider integrations.
- AI prompt versioning.
- Payment webhook idempotency.

Backend standards:
- Controllers should be thin.
- Application services/handlers should coordinate use cases.
- Domain entities should protect important invariants.
- Use async methods for I/O.
- Use cancellation tokens on API and database calls where practical.
- Avoid leaking EF entities directly to API responses.
- Use DTOs for requests and responses.

Frontend standards:
- Use Angular standalone components if the project standard supports them.
- Use reactive forms for complex forms.
- Use route guards for protected areas.
- Use services for API calls.
- Do not store sensitive tokens insecurely.
- Use shared UI components for product cards, status badges, and forms.

Acceptance criteria:
- The document is clear and enforceable.
- Include examples of good and bad practices.
```

---

# Part C — Backend Foundation Prompts

---

## Prompt 7 — Configure PostgreSQL and EF Core

```text
Context:
Mabuntle backend projects exist.

Goal:
Configure PostgreSQL with Entity Framework Core.

Requirements:
- Add EF Core and Npgsql packages to Mabuntle.Infrastructure.
- Create MabuntleDbContext.
- Configure dependency injection in Mabuntle.Api.
- Read connection string from configuration.
- Add a design-time DbContext factory if needed for migrations.
- Add initial migration.
- Create a simple database health check.
- Add integration test setup that can use PostgreSQL.

Do not:
- Do not add business entities yet unless required for DbContext setup.
- Do not add Identity yet.
- Do not add fake data beyond basic migration test.

Acceptance criteria:
- dotnet build passes.
- dotnet test passes.
- EF migration can be created/applied.
- Backend starts with PostgreSQL connection configured.
```

---

## Prompt 8 — Add common backend building blocks

```text
Goal:
Add common backend building blocks for Mabuntle.

Add:
- Base auditable entity with Id, CreatedAtUtc, UpdatedAtUtc.
- Optional soft delete interface or fields where useful.
- Domain event base interface.
- Result or error response pattern for application services.
- API error response model.
- Request validation pattern.
- Date/time abstraction if useful.
- Current user abstraction for accessing UserId and roles.
- Audit log entity placeholder.

Requirements:
- Keep implementation simple.
- Avoid overengineering.
- Do not add MediatR unless the architecture already uses it or the benefit is clear.
- If using MediatR, document why and wire it consistently.
- Add tests for Result/error pattern if implemented.

Acceptance criteria:
- Common types are reusable.
- API can return consistent errors.
- No business feature is implemented yet.
```

---

## Prompt 9 — Add CI pipeline

```text
Goal:
Add GitHub Actions CI for Mabuntle.

Requirements:
- Backend job:
  - dotnet restore
  - dotnet build
  - dotnet test
- Frontend job:
  - npm ci or npm install based on lockfile
  - npm run build
  - npm test if tests are configured
- Use PostgreSQL service container for integration tests if already needed.
- Do not include secrets.
- Add workflow file under .github/workflows/ci.yml.

Acceptance criteria:
- CI is clear and minimal.
- CI can run on pull requests.
- README mentions CI.
```

---

# Part D — Identity, Roles, and Access Control Prompts

---

## Prompt 10 — Implement identity and roles

```text
Context:
Mabuntle is a transactional ecommerce marketplace.

Goal:
Implement authentication and roles.

Roles:
- Buyer
- Seller
- Admin
- SuperAdmin
- SupportAgent

Requirements:
- Use ASP.NET Core Identity or a clear equivalent.
- Use JWT access tokens.
- Use refresh tokens.
- Add registration endpoint.
- Add login endpoint.
- Add refresh token endpoint.
- Add logout/revoke refresh token endpoint.
- Add current user endpoint.
- Add role-based authorization policies.
- Add email verification placeholder but do not integrate an email provider yet unless simple.
- Add tests for login and role restrictions.

Database:
- Identity user tables.
- Refresh token storage.
- Buyer profile created for buyers.
- Seller profile created for sellers, but seller starts as PendingVerification.

Rules:
- Users can register as Buyer or Seller.
- Seller accounts start with verification status Pending.
- Admin roles should not be self-assignable from public registration.
- Do not expose password hashes.

Acceptance criteria:
- Buyer can register and login.
- Seller can register and login.
- Seller has PendingVerification status by default.
- Admin-only endpoint rejects non-admin users.
- Seller-only endpoint rejects buyers.
- dotnet test passes.
```

---

## Prompt 11 — Implement Angular auth screens and guards

```text
Context:
The backend has auth endpoints.

Goal:
Implement Angular authentication UI and route protection.

Requirements:
- Create login page.
- Create buyer registration page.
- Create seller registration page.
- Create auth service for API calls.
- Store auth state safely.
- Add route guards for:
  - buyer routes
  - seller routes
  - admin routes
- Add logout.
- Add current user loading.
- Add basic validation and user-friendly error messages.
- Use Mabuntle colour palette.

Do not:
- Do not implement full seller onboarding yet.
- Do not implement social login yet.
- Do not store secrets in frontend.

Acceptance criteria:
- Buyer can login and access buyer routes.
- Seller can login and access seller routes.
- Admin routes are blocked for non-admins.
- Auth errors are displayed clearly.
- Angular build passes.
```

---

# Part E — Seller Onboarding and Storefront Prompts

---

## Prompt 12 — Implement seller profile and verification domain

```text
Goal:
Implement seller profile and verification entities.

Seller statuses:
- PendingVerification
- Verified
- Rejected
- Suspended
- UnderReview

Entities:
- SellerProfile
- SellerStorefront
- SellerVerification
- SellerAddress
- SellerBankAccountPlaceholder or SellerPayoutProfilePlaceholder

SellerProfile fields:
- UserId
- DisplayName
- ContactEmail
- PhoneNumber
- BusinessType
- BusinessName
- VerificationStatus
- CreatedAtUtc
- UpdatedAtUtc

SellerStorefront fields:
- SellerId
- StoreName
- Slug
- Description
- LogoUrl
- BannerUrl
- IsPublished

Rules:
- Seller storefront slug must be unique.
- Seller cannot be Verified without required onboarding fields.
- Seller payout fields should not be treated as verified until admin approval.
- Do not store full sensitive bank data unless encrypted/tokenized; for MVP use placeholders or provider references.

Acceptance criteria:
- EF entities and configurations are created.
- Migrations are added.
- Domain/application tests cover seller status rules.
- No payment provider integration yet.
```

---

## Prompt 13 — Seller onboarding API

```text
Goal:
Create seller onboarding API endpoints.

Endpoints:
- GET /api/seller/onboarding
- PUT /api/seller/onboarding/profile
- PUT /api/seller/onboarding/storefront
- PUT /api/seller/onboarding/address
- POST /api/seller/onboarding/submit-verification

Rules:
- Only authenticated sellers can access these endpoints.
- Seller can only update their own profile.
- Store slug must be unique.
- Seller can submit verification only if required fields are complete.
- Submitting verification changes status to UnderReview.
- Admin approval is not part of this task.

Acceptance criteria:
- API endpoints implemented.
- Request/response DTOs are used.
- Validation is implemented.
- Tests cover ownership and required fields.
```

---

## Prompt 14 — Angular seller onboarding wizard

```text
Goal:
Create Angular seller onboarding wizard for Mabuntle.

Steps:
1. Basic seller details
2. Storefront details
3. Address and fulfilment information
4. Payout information placeholder
5. Review and submit

Requirements:
- Use Angular Reactive Forms.
- Use seller onboarding API endpoints.
- Show verification status.
- Prevent submission if required fields are missing.
- Use Mabuntle colour palette.
- Provide helpful empty states and validation messages.

Do not:
- Do not integrate real payment/payout provider yet.
- Do not implement admin approval screen in this task.

Acceptance criteria:
- Seller can complete onboarding.
- Seller can submit for verification.
- Angular build passes.
```

---

## Prompt 15 — Admin seller approval

```text
Goal:
Create admin seller verification workflow.

Backend endpoints:
- GET /api/admin/sellers/pending
- GET /api/admin/sellers/{sellerId}
- POST /api/admin/sellers/{sellerId}/approve
- POST /api/admin/sellers/{sellerId}/reject
- POST /api/admin/sellers/{sellerId}/suspend

Rules:
- Only Admin or SuperAdmin can use these endpoints.
- Approval changes seller status to Verified.
- Rejection requires a reason.
- Suspension requires a reason.
- Every admin action must create an audit log entry.

Frontend:
- Create admin seller approval list.
- Create seller review detail page.
- Add approve/reject/suspend actions.
- Show audit trail if available.

Acceptance criteria:
- Admin can approve or reject sellers.
- Non-admin users cannot access endpoints or pages.
- Audit logs are written.
- Tests cover authorization and status transitions.
```

---

# Part F — Catalog, Products, Variants, Images, and Inventory Prompts

---

## Prompt 16 — Implement categories and category attributes

```text
Goal:
Implement Mabuntle category and category attribute system.

Categories should support:
- Parent/child hierarchy.
- Slug.
- Display order.
- Active/inactive status.

Category examples:
- Women > Clothing > Dresses
- Women > Clothing > Tops
- Men > Clothing > Shirts
- Jewellery > Earrings > Hoop Earrings
- Jewellery > Rings
- Accessories > Bags
- Accessories > Belts
- Beauty > Makeup > Foundation
- Beauty > Skincare > Cleansers

CategoryAttribute should support:
- Attribute name.
- Data type: Text, Number, Decimal, Boolean, Select, MultiSelect, Date.
- Required/optional.
- Allowed values for select fields.
- Applies to category.
- Display order.

Rules:
- Product attributes must be valid for the selected category.
- Required category attributes must be present before product submission.

Acceptance criteria:
- Entities, EF configs, migrations.
- Seed initial categories and attributes.
- Admin can list categories through API.
- Tests cover required category attributes.
```

---

## Prompt 17 — Implement product aggregate and statuses

```text
Goal:
Implement product aggregate for Mabuntle.

Product statuses:
- Draft
- PendingReview
- Published
- Rejected
- Archived
- OutOfStock

Product fields:
- SellerId
- CategoryId
- BrandId optional
- Title
- Slug
- ShortDescription
- FullDescription
- Status
- RejectionReason optional
- CreatedAtUtc
- UpdatedAtUtc
- PublishedAtUtc optional

Rules:
- Product starts as Draft.
- Seller can edit only Draft or Rejected products.
- Product cannot be submitted for review without category, title, description, at least one image, and at least one active variant.
- Product cannot be Published by seller directly unless marketplace policy allows it; for MVP require admin approval.
- Product slug should be unique within seller store or globally, depending on chosen implementation.

Acceptance criteria:
- Entities, EF configuration, migration.
- Domain methods for status transitions.
- Unit tests for product status rules.
- No UI yet.
```

---

## Prompt 18 — Implement product variants

```text
Goal:
Implement product variants for fashion marketplace products.

ProductVariant fields:
- ProductId
- SKU
- Size
- Colour
- Price
- CompareAtPrice optional
- StockQuantity
- ReservedQuantity
- Status
- Barcode optional

Variant statuses:
- Active
- Inactive
- OutOfStock

Rules:
- StockQuantity cannot be negative.
- ReservedQuantity cannot exceed StockQuantity.
- Price must be positive.
- Product can have multiple variants by size/colour.
- Product publish validation requires at least one active variant with stock.

Acceptance criteria:
- Entities, EF configuration, migration.
- Application service or command for adding/updating variants.
- Tests for stock and validation rules.
```

---

## Prompt 19 — Implement product images

```text
Goal:
Implement product image records and upload placeholder for Mabuntle.

Requirements:
- ProductImage entity:
  - ProductId
  - Url
  - PublicId or StorageKey
  - AltText
  - SortOrder
  - IsPrimary
  - CreatedAtUtc
- Add backend endpoint to create image records after upload.
- Add abstraction for image storage provider.
- Add placeholder implementation or local fake provider for development.
- Do not store image binary data in PostgreSQL.
- Add validation that image belongs to seller’s product.

Future provider:
- Cloudinary or Azure Blob Storage.

Acceptance criteria:
- Seller can attach image records to own product draft.
- Product cannot have two primary images.
- Seller cannot attach images to another seller’s product.
- Tests cover ownership and primary image rules.
```

---

## Prompt 20 — Seller product draft API

```text
Goal:
Create seller product draft API.

Endpoints:
- POST /api/seller/products
- GET /api/seller/products
- GET /api/seller/products/{id}
- PUT /api/seller/products/{id}
- POST /api/seller/products/{id}/variants
- PUT /api/seller/products/{id}/variants/{variantId}
- POST /api/seller/products/{id}/images
- POST /api/seller/products/{id}/submit-review

Rules:
- Only verified sellers can submit products for review.
- Pending sellers may create drafts but cannot submit for publication unless policy allows; choose and document the rule.
- Seller can only access own products in seller endpoints.
- Product submission validates required fields, variants, images, and category attributes.

Acceptance criteria:
- Endpoints implemented with DTOs.
- Validation included.
- Authorization included.
- Tests cover seller ownership and submit-review validation.
```

---

## Prompt 21 — Angular seller product form

```text
Goal:
Build Angular seller product creation/editing form.

Pages:
- /seller/products
- /seller/products/new
- /seller/products/:id/edit

Form sections:
1. Basic details
2. Category and attributes
3. Images
4. Variants and stock
5. Shipping/return notes placeholder
6. Review and submit

Requirements:
- Use Angular Reactive Forms.
- Load categories and category attributes from API.
- Support dynamic attributes based on selected category.
- Support adding/removing variants.
- Support attaching product image records or upload placeholder.
- Show product status.
- Prevent submit-review if required fields are missing.
- Use Mabuntle design palette.

Acceptance criteria:
- Seller can create a product draft.
- Seller can add variants.
- Seller can attach images.
- Seller can submit for review when valid.
- Angular build passes.
```

---

# Part G — AI Fashion Product Listing Assistant Prompts

---

## Prompt 22 — Define AI product suggestion schema and database tables

```text
Goal:
Create the database schema and DTOs for Mabuntle's AI Fashion Product Listing Assistant.

Entities/tables:
- AiProductSuggestion
- AiSuggestionFieldAudit
- AiUsageLog
- AiPromptVersion

AiProductSuggestion fields:
- Id
- SellerId
- ProductId
- InputNotes
- InputImageIdsJson
- SuggestedTitle
- SuggestedShortDescription
- SuggestedFullDescription
- SuggestedCategoryId optional
- SuggestedCategoryPath
- SuggestedAttributesJson
- SuggestedTagsJson
- MissingFieldsJson
- RiskFlagsJson
- QualityScore
- ModelUsed
- PromptVersion
- Status
- CreatedAtUtc
- AcceptedAtUtc optional

AiSuggestionFieldAudit fields:
- SuggestionId
- FieldName
- AiValue
- SellerFinalValue
- WasAccepted
- WasEdited
- CreatedAtUtc

AiUsageLog fields:
- FeatureName
- UserId
- SellerId optional
- ModelUsed
- InputTokenEstimate optional
- OutputTokenEstimate optional
- CostEstimate optional
- LatencyMs
- Success
- ErrorMessage optional
- CreatedAtUtc

Rules:
- AI suggestions are drafts.
- Seller must approve/edit before applying to product.
- Store prompt version used.
- Do not store raw secrets.

Acceptance criteria:
- Entities and EF configs created.
- Migrations added.
- DTOs created for AI suggestion request/response.
- Tests cover basic persistence if applicable.
```

---

## Prompt 23 — Implement AI Listing Assistant service abstraction

```text
Context:
Mabuntle has product drafts, categories, category attributes, and AI suggestion tables.

Goal:
Implement backend AI service abstraction for the AI Fashion Product Listing Assistant.

Create:
- IAiListingAssistantService
- AiListingAssistantService
- IAiProviderClient or IOpenAiClient abstraction
- AiPromptBuilder
- AiSuggestionValidator
- AiUsageLogger

Requirements:
- The service accepts seller notes, product draft, known attributes, category hints, and image references.
- The service builds a prompt that instructs AI to return structured JSON.
- The service must not apply suggestions directly to the product.
- The service validates returned JSON before saving.
- The service logs usage and errors.
- Add a fake/local AI provider implementation for tests/development.
- Do not call OpenAI directly from controllers.
- Do not call OpenAI from Angular.

AI safety rules:
- Do not invent brand, material, authenticity, ingredients, expiry date, medical claims, or exact sizing.
- Add missing information to missingFields instead of inventing.
- Flag beauty claims and counterfeit-risk wording.
- Use marketplace category list and allowed attributes.

Acceptance criteria:
- Backend compiles.
- Service can be unit tested with fake AI provider.
- Invalid AI JSON is handled gracefully.
- AI usage is logged.
```

---

## Prompt 24 — Implement AI product suggestion endpoint

```text
Goal:
Add API endpoint for generating AI product listing suggestions.

Endpoint:
POST /api/seller/products/{productId}/ai-suggestions

Request fields:
- sellerNotes
- productTypeHint optional
- selectedCategoryId optional
- knownAttributes
- imageIds

Response fields:
- suggestionId
- recommendedTitle
- titleSuggestions
- shortDescription
- fullDescription
- suggestedCategoryId
- suggestedCategoryPath
- attributes
- tags
- seo
- imageAltText
- missingFields
- riskFlags
- qualityScore

Rules:
- Only authenticated sellers can call endpoint.
- Seller must own product draft.
- Product must be Draft or Rejected.
- Rate-limit or add placeholder for rate-limiting future work.
- Save suggestion to database.
- Do not apply suggestions automatically to product.

Acceptance criteria:
- Endpoint implemented.
- Authorization and ownership checked.
- Suggestion saved.
- Tests cover unauthorized access, wrong seller, and successful generation with fake AI provider.
```

---

## Prompt 25 — Apply AI suggestions to product draft

```text
Goal:
Implement endpoint for seller to apply selected AI suggestions to a product draft.

Endpoint:
POST /api/seller/products/{productId}/ai-suggestions/{suggestionId}/apply

Request:
- fieldsToApply: list of fields
- editedValues: optional object containing seller edits

Rules:
- Seller must own product.
- Suggestion must belong to same product and seller.
- Product must be editable.
- Backend must validate all applied values.
- Attributes must be valid for the product category.
- Risky claims should not be applied without confirmation or should keep product flagged for review.
- Create AiSuggestionFieldAudit records showing AI values vs seller final values.

Acceptance criteria:
- Seller can apply title, descriptions, category, attributes, tags, and alt text selectively.
- Audits are stored.
- Invalid category/attribute values are rejected.
- Tests cover partial apply and edited values.
```

---

## Prompt 26 — Angular AI Product Listing Assistant panel

```text
Goal:
Build Angular UI for Mabuntle's AI Fashion Product Listing Assistant inside the seller product form.

UI section name:
AI Product Assistant

Features:
- Seller enters short notes.
- Seller clicks Generate with AI.
- Show loading state.
- Show suggested title options.
- Show short and full descriptions.
- Show suggested category.
- Show suggested attributes.
- Show tags.
- Show missing fields checklist.
- Show risk flags.
- Show quality score.
- Allow seller to apply suggestions field by field.
- Allow seller to edit before applying.
- Show disclaimer: "AI suggestions are drafts. Please review and confirm all product details before publishing."

Requirements:
- Use existing product draft API.
- Use Angular Reactive Forms.
- Do not call OpenAI directly.
- Use Mabuntle AI styling:
  - AI badge background #F3D9D6
  - AI badge text #3A1D32
  - AI suggestion border #B76E79
  - Warning #B45309
  - Error #B42318
  - Success #0F766E

Acceptance criteria:
- Seller can generate suggestions.
- Seller can apply selected suggestions.
- Errors and risk flags are displayed clearly.
- Angular build passes.
```

---

## Prompt 27 — AI moderation rules for product listings

```text
Goal:
Implement basic AI/business-rule moderation for Mabuntle product listings.

Create:
- ProductModerationService
- AiModerationResult entity/table
- Business rule keyword checks
- Admin review flag creation

Moderation checks:
- Counterfeit-risk terms:
  - replica
  - AAA copy
  - mirror quality
  - designer inspired
  - Gucci style
  - Rolex style
  - dupe
- Beauty-risk claims:
  - cures acne
  - removes scars permanently
  - guaranteed skin whitening
  - medical-grade treatment
  - clinically proven
  - permanent results
- Missing beauty product fields:
  - ingredients
  - expiry date
  - batch number
  - sealed/unsealed status
- Unsafe text/images placeholder through AI moderation provider abstraction.

Rules:
- High-risk products go to NeedsAdminReview.
- Moderation should flag, not accuse.
- Store reason and detected terms.
- Admin can override moderation later.

Acceptance criteria:
- Moderation runs when seller submits product for review.
- Risk flags are stored.
- Product status changes appropriately.
- Tests cover keyword flags and beauty missing-field flags.
```

---

# Part H — Admin Moderation and Product Approval Prompts

---

## Prompt 28 — Admin product review workflow

```text
Goal:
Create admin product approval workflow.

Backend endpoints:
- GET /api/admin/products/pending-review
- GET /api/admin/products/{productId}
- POST /api/admin/products/{productId}/approve
- POST /api/admin/products/{productId}/reject
- POST /api/admin/products/{productId}/request-changes

Rules:
- Only Admin/SuperAdmin can approve/reject.
- Approval changes product to Published if seller is verified.
- Rejection requires reason.
- Request changes requires reason.
- Every action creates audit log.
- Products with unresolved high-risk moderation flags should not be approved unless admin explicitly overrides with reason.

Frontend:
- Admin product review queue.
- Product detail with images, seller, attributes, AI risk flags.
- Approve/reject/request changes actions.

Acceptance criteria:
- Admin can approve product.
- Admin can reject with reason.
- Audit logs are recorded.
- Tests cover authorization and status transitions.
```

---

## Prompt 29 — Admin audit logs

```text
Goal:
Implement admin audit logs for sensitive actions.

AuditLog fields:
- Id
- ActorUserId
- ActorRole
- ActionType
- EntityType
- EntityId
- PreviousValueJson optional
- NewValueJson optional
- Reason optional
- IpAddress optional
- CreatedAtUtc

Actions to audit:
- Seller approval/rejection/suspension.
- Product approval/rejection.
- Manual payment or ledger adjustment.
- Refund approval.
- Payout hold/release.
- Admin role changes.
- Ad campaign approval/rejection.

Requirements:
- Add audit logging service.
- Use it in existing admin workflows.
- Create admin endpoint to view audit logs with filters.

Acceptance criteria:
- Sensitive admin actions write audit logs.
- Admin can view audit logs.
- Tests cover audit creation for at least seller and product approval.
```

---

# Part I — Search and Discovery Prompts

---

## Prompt 30 — Implement basic product search API

```text
Goal:
Implement basic product search API using PostgreSQL first.

Endpoint:
GET /api/products/search

Filters:
- query
- categoryId
- sellerId
- minPrice
- maxPrice
- size
- colour
- brandId
- material
- inStock
- sort
- page
- pageSize

Rules:
- Only Published products should appear publicly.
- Out-of-stock handling should be clear.
- Filters should work with product variants where relevant.
- Sort options:
  - newest
  - price_asc
  - price_desc
  - relevance placeholder

Acceptance criteria:
- Search returns product cards.
- Filters work.
- Pagination works.
- Tests cover published-only visibility and basic filters.
```

---

## Prompt 31 — Angular shop and category pages

```text
Goal:
Create buyer-facing shop and category pages for Mabuntle.

Pages:
- /shop
- /category/:slug
- /product/:slug
- /seller/:storeSlug

Features:
- Product grid.
- Filter sidebar.
- Sort dropdown.
- Search input.
- Product card component.
- Empty state.
- Loading state.
- Pagination or infinite scroll.

Product card shows:
- Primary image.
- Title.
- Price.
- Compare-at price if available.
- Seller store name.
- Rating placeholder.
- Wishlist button placeholder.

Requirements:
- Use search API.
- Use Mabuntle design palette.
- Public product pages should be clean and image-first.

Acceptance criteria:
- Buyer can browse published products.
- Buyer can filter by category, price, size, colour, brand.
- Angular build passes.
```

---

## Prompt 32 — Add Typesense or Meilisearch integration placeholder

```text
Goal:
Prepare Mabuntle for dedicated search engine integration.

Choose one based on project preference:
- Typesense
or
- Meilisearch

Requirements:
- Create ISearchIndexService abstraction.
- Create ProductSearchDocument DTO.
- Add fake/local implementation if search engine is not configured.
- Add background job or application service to index product when published/updated.
- Do not break PostgreSQL search fallback.

ProductSearchDocument fields:
- ProductId
- SellerId
- Title
- Description
- CategoryPath
- Brand
- PriceMin
- PriceMax
- Sizes
- Colours
- Materials
- Tags
- InStock
- PublishedAtUtc

Acceptance criteria:
- Search indexing service abstraction exists.
- Product publish/update can call indexing service.
- Search can fall back to database if external search is unavailable.
```

---

## Prompt 33 — Add product embeddings and pgvector foundation

```text
Goal:
Prepare Mabuntle for AI semantic search and similar products using pgvector.

Requirements:
- Add pgvector extension support to PostgreSQL migration.
- Add ProductEmbedding entity/table:
  - ProductId
  - SourceText
  - Embedding vector
  - ModelUsed
  - CreatedAtUtc
- Add IAiEmbeddingService abstraction.
- Add fake embedding provider for tests.
- Add job/service to generate embedding when product is published.
- Do not implement buyer AI assistant yet.

Source text should include:
- Product title
- Description
- Category path
- Attributes
- Tags
- Variant info such as sizes and colours

Acceptance criteria:
- Table and service abstraction exist.
- Product embedding generation can be triggered.
- Tests use fake embedding provider.
```

---

# Part J — Cart, Checkout, Orders, and Inventory Prompts

---

## Prompt 34 — Implement single-seller cart

```text
Goal:
Implement MVP cart with single-seller checkout rule.

Entities:
- Cart
- CartItem

Rules:
- A buyer has one active cart.
- Cart can contain products from only one seller for MVP.
- If buyer tries to add product from another seller, return clear error or require cart replacement.
- Cart item references ProductVariant.
- Quantity must be positive.
- Quantity cannot exceed available stock minus reserved stock.
- Cart item price should capture current product price for display but final order price must be confirmed at checkout.

Endpoints:
- GET /api/cart
- POST /api/cart/items
- PUT /api/cart/items/{itemId}
- DELETE /api/cart/items/{itemId}
- DELETE /api/cart

Acceptance criteria:
- Buyer can add items to cart.
- Buyer cannot mix sellers in cart.
- Stock validation works.
- Tests cover seller-mixing rule and stock limits.
```

---

## Prompt 35 — Implement inventory reservations

```text
Goal:
Implement inventory reservation for checkout.

Entities:
- InventoryReservation

Fields:
- ProductVariantId
- BuyerId
- CartId
- Quantity
- ExpiresAtUtc
- Status

Statuses:
- Active
- Confirmed
- Expired
- Cancelled

Rules:
- Reservation is created when buyer starts checkout.
- Reservation expires after configured time, e.g. 15 minutes.
- Payment success confirms reservation and reduces available stock.
- Payment failure or checkout expiry releases reservation.
- Reserved quantity cannot exceed available stock.
- Use database transaction for reservation creation.

Acceptance criteria:
- Inventory reservation service implemented.
- Tests cover reservation, expiry, and insufficient stock.
- Background job placeholder exists for expiring reservations.
```

---

## Prompt 36 — Create order aggregate from cart

```text
Goal:
Implement order creation from cart for single-seller checkout.

Entities:
- Order
- OrderItem
- OrderStatusHistory

Order statuses:
- PendingPayment
- Paid
- Processing
- ReadyToShip
- Shipped
- Delivered
- ReturnRequested
- Refunded
- Cancelled
- Disputed
- Completed

Rules:
- Order is created from active cart after checkout starts.
- Order captures product title, variant details, price, seller, buyer, and quantity at time of order.
- Order total includes items, shipping, platform fees where relevant, and discounts placeholder.
- Order starts as PendingPayment.
- Order status changes must create status history.
- Buyer can only view own orders.
- Seller can only view orders containing their products.

Acceptance criteria:
- Order aggregate implemented.
- Create order from cart service implemented.
- Tests cover total calculation and status history.
```

---

## Prompt 37 — Angular cart and checkout UI

```text
Goal:
Create Angular cart and checkout UI for Mabuntle MVP.

Pages:
- /cart
- /checkout
- /checkout/success
- /checkout/failed

Features:
- Cart item list.
- Quantity update.
- Remove item.
- Single-seller notice.
- Order summary.
- Shipping address form placeholder.
- Payment initiation placeholder.

Rules:
- Show clear error if cart has stock issues.
- Show seller name for the cart.
- Checkout should call backend, not payment provider directly unless provider requires public checkout redirect.

Acceptance criteria:
- Buyer can view cart.
- Buyer can update quantities.
- Buyer can start checkout.
- Angular build passes.
```

---

# Part K — Payments, Ledger, Seller Balances, and Payout Prompts

---

## Prompt 38 — Payment provider abstraction

```text
Goal:
Create payment provider abstraction for Mabuntle.

Do not implement full provider logic yet unless provider is specified.

Create:
- IPaymentProvider
- PaymentInitiationRequest
- PaymentInitiationResult
- PaymentWebhookEvent
- PaymentProviderOptions

Provider methods:
- CreateCheckoutSession or InitializePayment
- VerifyPayment
- ParseWebhook
- VerifyWebhookSignature

Requirements:
- Add fake payment provider for tests/development.
- External payment calls must be in Infrastructure.
- Application layer should depend on abstraction only.
- Controllers should not contain provider-specific business logic.

Acceptance criteria:
- Payment provider abstraction exists.
- Fake provider can simulate success/failure.
- Unit tests cover application flow using fake provider.
```

---

## Prompt 39 — Implement payments and payment events

```text
Goal:
Implement Payment and PaymentEvent entities and payment initiation flow.

Payment statuses:
- Pending
- Authorized
- Paid
- Failed
- Cancelled
- Refunded
- PartiallyRefunded
- Disputed

Payment fields:
- OrderId
- BuyerId
- Provider
- ProviderReference
- Amount
- Currency
- Status
- CreatedAtUtc
- PaidAtUtc optional

PaymentEvent fields:
- PaymentId optional
- Provider
- ProviderEventId
- EventType
- RawPayloadJson
- ReceivedAtUtc
- ProcessedAtUtc optional
- ProcessingStatus

Rules:
- Payment initiation creates local Payment record.
- Payment provider reference is stored.
- Do not mark order Paid until provider confirms payment through verification/webhook.
- Payment events must be idempotent.

Acceptance criteria:
- Payment initiation endpoint exists.
- Fake payment provider works in tests.
- Tests cover successful initiation and failed initiation.
```

---

## Prompt 40 — Payment webhook idempotency

```text
Goal:
Implement payment webhook handling with idempotency.

Endpoint:
POST /api/payments/webhook/{provider}

Requirements:
- Verify webhook signature through provider abstraction.
- Store raw event payload.
- Use ProviderEventId or equivalent to prevent duplicate processing.
- Process event in transaction.
- On payment success:
  - Mark Payment as Paid.
  - Mark Order as Paid.
  - Confirm inventory reservations.
  - Create ledger entries.
- On payment failure:
  - Mark Payment as Failed.
  - Release inventory reservations.
  - Keep order as Cancelled or PaymentFailed depending on chosen status.

Acceptance criteria:
- Duplicate webhook does not duplicate ledger entries.
- Successful webhook updates payment/order once.
- Failed webhook releases reservation.
- Tests cover duplicate webhook.
```

---

## Prompt 41 — Internal marketplace ledger

```text
Goal:
Implement Mabuntle internal ledger.

Entities:
- LedgerEntry
- SellerBalance
- CommissionRule

LedgerEntry fields:
- Id
- OrderId optional
- OrderItemId optional
- SellerId optional
- BuyerId optional
- PaymentId optional
- Type
- Amount
- Currency
- Direction
- Description
- CreatedAtUtc

Ledger entry types:
- BuyerPaymentReceived
- PlatformCommissionRecorded
- PaymentProviderFeeRecorded
- SellerPendingBalanceCredited
- SellerBalanceHeld
- SellerBalanceAvailable
- SellerPayoutReleased
- RefundIssued
- RefundReversal
- ManualAdjustment

Rules:
- Ledger entries are append-only.
- Do not update ledger entry amounts after creation.
- Use reversal entries for corrections.
- Seller balance is calculated from ledger or maintained with careful transactions.
- Platform transaction fee should be configurable.

Acceptance criteria:
- Ledger service creates entries for successful payment.
- Seller pending balance is credited correctly.
- Tests cover commission calculation and ledger idempotency.
```

---

## Prompt 42 — Seller balances and payout workflow

```text
Goal:
Implement seller balance and payout workflow.

Payout statuses:
- Pending
- OnHold
- Available
- Processing
- PaidOut
- Reversed
- Failed

Rules:
- Seller balance becomes Pending after payment.
- Balance should not become Available until order is delivered and return/dispute window policy is satisfied.
- Payout can be held for disputes, high-risk sellers, or admin review.
- Admin can manually hold/release with reason.
- Every payout action creates audit log.

Entities:
- SellerPayout
- SellerPayoutItem
- SellerBalanceSnapshot optional

Endpoints:
- GET /api/seller/balance
- GET /api/seller/payouts
- GET /api/admin/payouts/pending
- POST /api/admin/payouts/{id}/hold
- POST /api/admin/payouts/{id}/release

Acceptance criteria:
- Seller can view pending/available balances.
- Admin can hold/release payouts.
- Audit logs are created.
- Tests cover payout hold rules.
```

---

# Part L — Shipping, Returns, Refunds, and Disputes Prompts

---

## Prompt 43 — Manual shipping and fulfilment

```text
Goal:
Implement MVP manual shipping and fulfilment.

Entities:
- Shipment
- ShipmentEvent

Shipment statuses:
- AwaitingFulfilment
- Packed
- ReadyForCourier
- Collected
- InTransit
- Delivered
- DeliveryFailed
- ReturnedToSender

Seller endpoints:
- GET /api/seller/orders
- GET /api/seller/orders/{orderId}
- POST /api/seller/orders/{orderId}/mark-processing
- POST /api/seller/orders/{orderId}/mark-shipped
- POST /api/seller/orders/{orderId}/tracking

Buyer endpoints:
- GET /api/buyer/orders
- GET /api/buyer/orders/{orderId}

Rules:
- Seller can only manage own orders.
- Buyer can only view own orders.
- Uploading tracking number creates shipment event.
- Marking as delivered may be admin/manual for MVP.

Acceptance criteria:
- Seller can manage fulfilment.
- Buyer can track order status.
- Tests cover authorization and status transitions.
```

---

## Prompt 44 — Return request workflow

```text
Goal:
Implement return request workflow for Mabuntle.

Entities:
- ReturnRequest
- ReturnItem
- ReturnMessage optional

Return statuses:
- Requested
- AwaitingSellerResponse
- Approved
- Rejected
- ReturnInTransit
- ReturnedToSeller
- RefundPending
- Refunded
- Disputed
- Closed

Return reasons:
- WrongSize
- WrongItem
- DamagedItem
- NotAsDescribed
- CounterfeitConcern
- ExpiredBeautyProduct
- AllergicReactionConcern
- ChangedMind
- LateDelivery
- Other

Rules:
- Buyer can request return for own delivered order.
- Beauty products may have stricter rules if opened/unsealed.
- Seller can respond.
- Admin can escalate/override.
- Return request should place seller payout on hold.

Acceptance criteria:
- Buyer can request return.
- Seller can respond.
- Admin can view disputed returns.
- Seller payout is held when return is active.
- Tests cover return eligibility and payout hold.
```

---

## Prompt 45 — Refund workflow and ledger reversals

```text
Goal:
Implement refund workflow with ledger reversals.

Entities:
- Refund
- RefundEvent

Refund statuses:
- Requested
- Approved
- Processing
- Refunded
- Failed
- Rejected

Rules:
- Refund can be full or partial.
- Refund approval must create ledger reversal entries.
- Seller pending/available balance must be adjusted.
- If seller has already been paid out, create negative balance or manual recovery record.
- Refund provider call should use payment provider abstraction.
- Admin action requires reason and audit log.

Acceptance criteria:
- Admin can approve refund.
- Ledger reversals are created.
- Seller balance adjusts correctly.
- Tests cover full and partial refund calculations.
```

---

## Prompt 46 — Dispute workflow

```text
Goal:
Implement dispute workflow for Mabuntle.

Entities:
- Dispute
- DisputeMessage
- DisputeEvidence

Dispute statuses:
- Open
- AwaitingBuyer
- AwaitingSeller
- UnderAdminReview
- ResolvedBuyerFavoured
- ResolvedSellerFavoured
- Closed

Rules:
- Buyer can open dispute for eligible order/return.
- Seller can respond.
- Admin can make final decision.
- Active dispute holds seller payout.
- Evidence can include text and image/file references.
- Sensitive admin actions must be audited.

Acceptance criteria:
- Buyer can open dispute.
- Seller can respond.
- Admin can resolve dispute.
- Payout hold is applied during active dispute.
- Tests cover authorization and status transitions.
```

---

# Part M — Admin and Support Prompts

---

## Prompt 47 — Admin dashboard foundation

```text
Goal:
Create admin dashboard foundation for Mabuntle.

Backend endpoints:
- GET /api/admin/dashboard/summary

Summary metrics:
- Pending seller approvals
- Pending product reviews
- New orders today
- Open disputes
- Pending refunds
- Pending payouts
- Total gross sales placeholder
- Platform commission placeholder

Frontend:
- Admin dashboard page.
- Cards for metrics.
- Navigation to sellers, products, orders, payments, refunds, disputes, payouts, ads.

Rules:
- Admin only.
- Do not expose sensitive buyer/seller details unnecessarily.

Acceptance criteria:
- Admin can access dashboard.
- Non-admin cannot access dashboard.
- Angular build and backend tests pass.
```

---

## Prompt 48 — Support ticket system

```text
Goal:
Implement basic support ticket system.

Entities:
- SupportTicket
- SupportMessage

Ticket categories:
- OrderIssue
- PaymentIssue
- ReturnIssue
- SellerIssue
- ProductIssue
- TechnicalIssue
- Other

Statuses:
- Open
- WaitingForCustomer
- WaitingForSeller
- Escalated
- Resolved
- Closed

Rules:
- Buyer can create support ticket.
- Seller can create support ticket.
- SupportAgent/Admin can respond.
- Ticket can be linked to order, product, seller, or payment.
- Internal admin notes should not be visible to users.

Acceptance criteria:
- Buyer/seller can create ticket.
- Support/admin can respond.
- Internal notes are private.
- Tests cover visibility rules.
```

---

# Part N — Seller Advertising and Monetization Prompts

---

## Prompt 49 — Ad campaign domain model

```text
Goal:
Implement seller ad campaign domain model for Mabuntle.

Context:
Mabuntle MVP is free for sellers to join and list products. Revenue comes from transaction fees first. Later, sellers can pay for promoted listings and ad campaigns.

Entities:
- AdCampaign
- AdCampaignProduct
- AdBudget
- AdImpression
- AdClick
- AdConversion
- AdCharge
- SellerAdCredit

Campaign statuses:
- Draft
- PendingReview
- Active
- Paused
- Completed
- Rejected
- Cancelled

Campaign types:
- FeaturedProduct
- SponsoredSearch
- FeaturedStorefront
- CategorySpotlight

Rules:
- Seller must be verified.
- Product must be published.
- Product must be in stock.
- Product must not have moderation flags.
- Product quality score must meet minimum threshold.
- Seller under suspension or serious dispute cannot create campaign.
- Campaign must have budget and dates.

Acceptance criteria:
- Entities and EF configs created.
- Campaign eligibility service implemented.
- Unit tests cover eligibility rules.
```

---

## Prompt 50 — Seller ad campaign API

```text
Goal:
Create seller API for ad campaigns.

Endpoints:
- POST /api/seller/ad-campaigns
- GET /api/seller/ad-campaigns
- GET /api/seller/ad-campaigns/{id}
- PUT /api/seller/ad-campaigns/{id}
- POST /api/seller/ad-campaigns/{id}/submit-review
- POST /api/seller/ad-campaigns/{id}/pause
- POST /api/seller/ad-campaigns/{id}/resume
- POST /api/seller/ad-campaigns/{id}/cancel

Rules:
- Seller can only manage own campaigns.
- Campaign cannot promote ineligible products.
- Campaign cannot run without budget.
- Campaign cannot be active until approved by admin or auto-approved by policy.

Acceptance criteria:
- Seller can create draft campaign.
- Eligibility is validated.
- Seller cannot manage another seller’s campaign.
- Tests cover authorization and eligibility.
```

---

## Prompt 51 — Admin ad campaign review

```text
Goal:
Create admin review workflow for seller ad campaigns.

Endpoints:
- GET /api/admin/ad-campaigns/pending
- GET /api/admin/ad-campaigns/{id}
- POST /api/admin/ad-campaigns/{id}/approve
- POST /api/admin/ad-campaigns/{id}/reject

Rules:
- Admin can approve/reject campaigns.
- Rejection requires reason.
- Approval requires products still eligible.
- Every admin action creates audit log.

Frontend:
- Admin campaign review queue.
- Campaign detail page showing seller, products, budget, dates, eligibility checks.
- Approve/reject buttons.

Acceptance criteria:
- Admin can approve/reject campaign.
- Audit logs are created.
- Tests cover status transitions.
```

---

## Prompt 52 — Track ad impressions, clicks, and conversions

```text
Goal:
Implement ad tracking for Mabuntle promoted listings.

Requirements:
- Track ad impressions when sponsored product is shown.
- Track ad clicks when buyer clicks sponsored product.
- Track conversion when buyer purchases after ad click within attribution window.
- Store enough metadata for campaign reporting.

Rules:
- Avoid overcounting repeated impressions/clicks where possible.
- Do not expose personal data unnecessarily in ad reporting.
- Campaign must be Active to record billable events.
- Out-of-stock products should not be shown as ads.

Seller campaign metrics:
- Impressions
- Clicks
- Click-through rate
- Spend
- Orders generated
- Revenue generated
- Return on ad spend

Acceptance criteria:
- Impression and click events are stored.
- Campaign dashboard can query metrics.
- Conversion attribution service exists.
- Tests cover active campaign only and ineligible campaign exclusion.
```

---

## Prompt 53 — Seller ad campaign dashboard UI

```text
Goal:
Create Angular seller ad campaign dashboard.

Pages:
- /seller/ads
- /seller/ads/new
- /seller/ads/:id

Features:
- List campaigns.
- Create campaign wizard.
- Select products to promote.
- Set campaign type.
- Set start/end date.
- Set daily or total budget.
- Show eligibility warnings.
- Show campaign metrics.
- Pause/resume/cancel actions.

Use Mabuntle design palette.

Acceptance criteria:
- Seller can create campaign draft.
- Seller can submit campaign for review.
- Seller can view metrics.
- Angular build passes.
```

---

# Part O — Analytics and Reporting Prompts

---

## Prompt 54 — Seller analytics dashboard

```text
Goal:
Create seller analytics dashboard.

Metrics:
- Total sales.
- Orders.
- Average order value.
- Conversion rate placeholder.
- Products sold.
- Top products.
- Low-stock products.
- Refund rate.
- Return rate.
- Ad campaign performance if available.
- AI listing assistant usage if available.

Rules:
- Seller can see only own analytics.
- Use aggregated queries.
- Avoid exposing buyer personal data.

Acceptance criteria:
- Seller dashboard endpoint returns metrics.
- Angular dashboard displays cards and tables.
- Tests cover seller isolation.
```

---

## Prompt 55 — Admin finance and marketplace reports

```text
Goal:
Create admin reports for marketplace operations and finance.

Reports:
- Gross merchandise value.
- Platform commission earned.
- Payment processing fees placeholder.
- Refunds.
- Seller pending balances.
- Seller available balances.
- Payouts processed.
- Failed payouts.
- Disputes.
- Top sellers.
- Top categories.

Rules:
- Admin only.
- Use date range filters.
- Export CSV placeholder or implementation.

Acceptance criteria:
- Admin can view report summary.
- Admin can filter by date range.
- Tests cover admin-only access.
```

---

## Prompt 56 — AI usage analytics

```text
Goal:
Create AI usage analytics for Mabuntle.

Metrics:
- AI product suggestions generated.
- AI suggestion acceptance rate.
- Average listing quality score.
- Average quality score improvement.
- AI moderation flags.
- AI failures.
- AI latency.
- Estimated AI cost.

Admin view:
- AI usage dashboard.
- Filter by date range.
- Filter by feature.
- Filter by seller.

Seller view:
- Products improved with AI.
- AI suggestions accepted.
- Listing quality score improvements.

Acceptance criteria:
- AI usage logs are aggregated.
- Admin can view platform AI usage.
- Seller can view own AI benefit metrics.
```

---

# Part P — Security, Privacy, Reliability, and Performance Prompts

---

## Prompt 57 — Add API authorization audit

```text
Goal:
Review and harden authorization across Mabuntle backend.

Tasks:
- Scan all API endpoints.
- Ensure public endpoints are intentionally public.
- Ensure seller endpoints require Seller role.
- Ensure admin endpoints require Admin/SuperAdmin/SupportAgent as appropriate.
- Ensure seller ownership checks exist for products, orders, campaigns, payouts, and analytics.
- Ensure buyer ownership checks exist for carts, orders, returns, disputes, wishlists.
- Add missing tests for authorization.

Acceptance criteria:
- Document endpoints reviewed.
- Add tests for at least five critical authorization cases.
- No seller can access another seller’s data.
- No buyer can access another buyer’s order.
```

---

## Prompt 58 — Add rate limiting and abuse protection placeholders

```text
Goal:
Add basic rate limiting and abuse protection placeholders.

Areas:
- Login attempts.
- Registration.
- AI suggestion generation.
- Product creation.
- Payment initiation.
- Ad click tracking.
- Search endpoint.

Requirements:
- Implement framework-level rate limiting if available.
- Add configurable limits.
- Return clear error responses.
- Do not block normal local development harshly.

Acceptance criteria:
- Rate limiting is configured.
- AI endpoint has stricter limit than normal browsing endpoints.
- Tests or documented manual checks exist.
```

---

## Prompt 59 — Add webhook security and idempotency review

```text
Goal:
Review payment webhook security and idempotency.

Tasks:
- Ensure webhook signature verification is required.
- Ensure raw payload is used for signature validation if provider requires it.
- Ensure duplicate webhook events are not processed twice.
- Ensure webhook event processing is transactional.
- Ensure failure is logged without exposing secrets.
- Ensure retries do not duplicate ledger entries.

Acceptance criteria:
- Add or update tests for duplicate webhooks.
- Add or update tests for invalid signature.
- Document webhook handling in docs/api-contracts.md or docs/payments.md.
```

---

## Prompt 60 — Add observability foundation

```text
Goal:
Add observability foundation to Mabuntle backend.

Requirements:
- Structured logging.
- Correlation ID support.
- Request logging.
- Error logging.
- Health checks for database.
- Optional health checks for search/storage/payment provider placeholders.
- Metrics placeholders for payments, AI usage, orders, and errors.

Do not:
- Do not add a heavy monitoring vendor unless already chosen.

Acceptance criteria:
- Logs include correlation ID.
- Health endpoint reports database status.
- Errors are logged consistently.
```

---

# Part Q — Buyer AI Assistant, Visual Search, and Personalization Prompts

These should be used later after core marketplace, search, and product catalog are stable.

---

## Prompt 61 — Buyer AI shopping assistant intent extraction

```text
Goal:
Implement backend intent extraction for buyer AI shopping assistant.

Example buyer prompts:
- I need an outfit for a wedding under R1,500.
- Find gold earrings for sensitive ears.
- Show me a black dress in size medium.
- I need skincare for oily skin under R300.

Create:
- IAiShoppingIntentService
- ShoppingIntent DTO
- Fake AI provider for tests

ShoppingIntent fields:
- Category
- Subcategory
- BudgetMax
- BudgetMin
- Size
- Colour
- Occasion
- Style
- Material
- Brand
- BeautySkinType
- BeautyConcern
- SearchText

Rules:
- AI extracts intent only.
- AI must not invent products.
- Backend search finds products.
- AI can summarize only products returned by backend.

Acceptance criteria:
- Intent extraction service exists.
- Tests use fake provider.
- Invalid or vague intent handled gracefully.
```

---

## Prompt 62 — Buyer AI shopping assistant product recommendations

```text
Goal:
Implement buyer AI shopping assistant that recommends real Mabuntle products only.

Flow:
1. Buyer submits message.
2. AI extracts shopping intent.
3. Backend searches published/in-stock products.
4. Backend returns product candidates.
5. AI summarizes why candidates match.
6. Frontend displays product cards.

Rules:
- Do not allow AI to invent products, prices, sellers, delivery times, or stock.
- AI response must reference product IDs returned by backend.
- If no products match, AI should suggest adjusted filters.
- Beauty advice must stay product-discovery focused and avoid medical advice.

Acceptance criteria:
- Backend endpoint implemented.
- Product cards returned.
- Tests verify response uses only product IDs from search results.
```

---

## Prompt 63 — Visual search MVP

```text
Goal:
Implement MVP visual search for Mabuntle.

Flow:
1. Buyer uploads image or provides image reference.
2. AI vision describes the item.
3. Backend extracts searchable attributes:
   - category
   - colour
   - style
   - shape
   - pattern
   - material guess with low confidence if not certain
4. Backend searches products.
5. Results are shown as product cards.

Rules:
- Do not claim exact material or brand from image unless clear and verified.
- Visual search should search real products only.
- Store no user image longer than necessary unless policy allows.

Acceptance criteria:
- Visual search endpoint exists.
- Fake vision provider can be used in tests.
- Angular UI allows image upload/search.
```

---

# Part R — Code Review, Refactoring, and Debugging Prompts

---

## Prompt 64 — Codex PR review prompt

```text
Review this Mabuntle pull request as a senior full-stack engineer.

Focus on:
- Security risks.
- Authorization mistakes.
- Seller/buyer data isolation.
- Payment and ledger correctness.
- Status transition correctness.
- Missing tests.
- Database migration issues.
- Angular form and route guard issues.
- Performance problems.
- Unclear naming.
- Any deviation from AGENTS.md.

Do not nitpick style unless it affects maintainability.
Prioritize serious issues that should block merge.
For each issue, include:
- File/location.
- Why it matters.
- Suggested fix.
- Whether it is blocking or non-blocking.
```

---

## Prompt 65 — Security review prompt

```text
Perform a focused security review of the current Mabuntle codebase.

Focus areas:
- Authentication.
- JWT handling.
- Refresh token handling.
- Role-based authorization.
- Seller ownership checks.
- Buyer ownership checks.
- Admin-only actions.
- File upload validation.
- Payment webhook signature validation.
- Secret handling.
- CORS configuration.
- Rate limiting.

Output:
- Critical issues.
- High issues.
- Medium issues.
- Low issues.
- Suggested remediation tasks.
- Tests that should be added.

Do not change code unless explicitly asked. This is a review task only.
```

---

## Prompt 66 — Payment and ledger review prompt

```text
Review Mabuntle's payment, ledger, seller balance, refund, and payout logic.

Check:
- Webhook idempotency.
- Duplicate event handling.
- Ledger append-only behavior.
- Commission calculation.
- Payment provider fee recording.
- Seller pending balance creation.
- Seller payout hold rules.
- Refund ledger reversal.
- Partial refund behavior.
- Order status transitions.
- Race conditions.

Output:
- Blocking correctness issues.
- Missing tests.
- Edge cases.
- Suggested refactors.
- Documentation updates needed.

Do not change code unless explicitly asked.
```

---

## Prompt 67 — Refactor prompt template

```text
Refactor the specified Mabuntle module without changing behavior.

Module:
[INSERT MODULE]

Goals:
- Improve readability.
- Reduce duplication.
- Keep public API behavior unchanged.
- Preserve tests.
- Add tests if behavior is currently untested.

Constraints:
- Do not change database schema unless necessary.
- Do not change API contracts unless explicitly requested.
- Do not introduce new packages unless justified.
- Do not touch unrelated modules.

Acceptance criteria:
- Existing tests pass.
- New tests added if needed.
- Explain what changed and why.
```

---

## Prompt 68 — Bug fix prompt template

```text
Fix this bug in Mabuntle.

Bug:
[DESCRIBE BUG]

Expected behavior:
[DESCRIBE EXPECTED]

Actual behavior:
[DESCRIBE ACTUAL]

Known affected area:
[MODULE/PAGE/ENDPOINT]

Requirements:
- Reproduce or reason about the bug.
- Add a failing test first if practical.
- Fix the bug.
- Run relevant tests.
- Do not change unrelated behavior.

Output:
- Root cause.
- Files changed.
- Tests added/updated.
- Tests run.
- Any follow-up concerns.
```

---

## Prompt 69 — Documentation update prompt

```text
Update Mabuntle documentation after the latest code changes.

Docs to review:
- docs/architecture.md
- docs/database-schema.md
- docs/api-contracts.md
- docs/feature-roadmap.md
- docs/coding-standards.md

Requirements:
- Reflect actual implemented code.
- Add new endpoints and DTOs.
- Add new database tables/fields.
- Add new business rules.
- Remove outdated statements.
- Keep documentation concise but complete.

Acceptance criteria:
- Documentation matches code changes.
- No unsupported claims.
```

---

# Part S — Suggested Development Sequence

Use this sequence with Codex:

1. Prompt 1 — Initial monorepo.
2. Prompt 2 — AGENTS.md.
3. Prompt 3 — Dev environment and health checks.
4. Prompt 4 — Architecture documentation.
5. Prompt 5 — Feature roadmap.
6. Prompt 7 — PostgreSQL and EF Core.
7. Prompt 8 — Common backend building blocks.
8. Prompt 9 — CI pipeline.
9. Prompt 10 — Identity and roles.
10. Prompt 11 — Angular auth.
11. Prompt 12 — Seller profile and verification domain.
12. Prompt 13 — Seller onboarding API.
13. Prompt 14 — Angular seller onboarding wizard.
14. Prompt 15 — Admin seller approval.
15. Prompt 16 — Categories and attributes.
16. Prompt 17 — Product aggregate.
17. Prompt 18 — Product variants.
18. Prompt 19 — Product images.
19. Prompt 20 — Seller product draft API.
20. Prompt 21 — Angular seller product form.
21. Prompt 22 — AI suggestion schema.
22. Prompt 23 — AI service abstraction.
23. Prompt 24 — AI suggestion endpoint.
24. Prompt 25 — Apply AI suggestions.
25. Prompt 26 — Angular AI panel.
26. Prompt 27 — AI moderation rules.
27. Prompt 28 — Admin product review.
28. Prompt 30 — Basic product search.
29. Prompt 31 — Angular shop/category/product pages.
30. Prompt 34 — Single-seller cart.
31. Prompt 35 — Inventory reservations.
32. Prompt 36 — Create order from cart.
33. Prompt 37 — Angular cart/checkout UI.
34. Prompt 38 — Payment provider abstraction.
35. Prompt 39 — Payments and events.
36. Prompt 40 — Webhook idempotency.
37. Prompt 41 — Internal ledger.
38. Prompt 42 — Seller balances and payouts.
39. Prompt 43 — Manual shipping.
40. Prompt 44 — Returns.
41. Prompt 45 — Refunds.
42. Prompt 46 — Disputes.
43. Prompt 47 — Admin dashboard.
44. Prompt 48 — Support tickets.
45. Prompt 49 onwards — Advertising campaigns once core transactions are stable.

---

# Part T — Important implementation advice

## Do not build these too early

- Multi-seller checkout.
- Complex ad platform.
- Virtual try-on.
- Full AI shopping assistant.
- Advanced personalization.
- Complex subscription plans.
- Fully automated counterfeit detection.

## Build these early

- Seller verification status.
- Product variants.
- Inventory reservation.
- Internal ledger.
- Payment webhook idempotency.
- Admin audit logs.
- Product moderation.
- Seller ownership checks.
- Clear product statuses.
- AI usage logging.

## Keep the first MVP focused

The first real MVP should prove:

- Sellers can onboard.
- Sellers can create good product listings.
- Buyers can discover products.
- Buyers can checkout safely.
- Payments are tracked correctly.
- Seller balances are calculated correctly.
- Admin can manage sellers/products/orders/refunds.
- AI improves seller listing quality.

---

# Part U — Copy/paste master prompt for any new Codex task

Use this before individual task instructions when you start a new Codex task.

```text
You are working on Mabuntle, a transactional ecommerce marketplace for fashion, clothing, jewellery, accessories, and beauty products.

Follow AGENTS.md and project documentation.

Architecture:
- Angular frontend.
- ASP.NET Core Web API backend.
- PostgreSQL database.
- EF Core with Npgsql.
- Modular monolith.
- Clean Architecture principles.
- Vertical slices for feature work.
- Pragmatic CQRS.
- EF Core direct usage for simple operations.
- Specific repositories/services only for complex workflows.

Important rules:
- Do not expose secrets.
- Do not call external providers from Angular.
- Do not make unrelated changes.
- Add tests for business rules.
- Keep controllers thin.
- Validate seller/buyer/admin authorization.
- Update docs if schema/API/business rules change.

Now implement this task:
[PASTE FEATURE-SPECIFIC PROMPT HERE]
```

---

# Part V — Prompt quality checklist

Before sending a prompt to Codex, make sure it includes:

- Project context.
- Specific goal.
- Scope.
- Requirements.
- Acceptance criteria.
- Do-not-do list.
- Tests expected.
- Documentation expected.
- Whether frontend, backend, database, or all layers are in scope.

A good Codex task should be narrow enough that you can review the diff carefully.

---

# Part W — References to review while configuring Codex

These are useful reference areas to review when setting up Codex workflows:

- OpenAI Codex overview: https://developers.openai.com/codex
- AGENTS.md guidance: https://developers.openai.com/codex/guides/agents-md
- Codex cloud environments: https://developers.openai.com/codex/cloud/environments
- Codex GitHub code reviews: https://developers.openai.com/codex/use-cases/github-code-reviews
- Codex best practices: https://developers.openai.com/codex/learn/best-practices

