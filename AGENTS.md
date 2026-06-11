# Mabuntle Agent Guide

## Project Overview

Mabuntle is a transactional ecommerce marketplace for fashion, clothing, jewellery, accessories, and beauty products. The codebase starts as a modular monolith with Clean Architecture boundaries.

## Tech Stack

- Frontend: Angular, TypeScript, Angular Router, SSR/hybrid rendering, Tailwind CSS, Angular Material.
- Backend: ASP.NET Core Web API, C#, .NET 9, Entity Framework Core, Npgsql/PostgreSQL.
- Database: PostgreSQL with pgvector support.
- Background work: ASP.NET Core Worker foundation now; Hangfire or dedicated jobs later.

## Architecture Rules

- Keep the backend as a modular monolith.
- Follow Clean Architecture dependencies:
  - `Mabuntle.Domain` depends on no infrastructure.
  - `Mabuntle.Application` depends on `Domain`.
  - `Mabuntle.Infrastructure` depends on `Application` and `Domain`.
  - `Mabuntle.Api` depends on `Application` and `Infrastructure`.
  - `Mabuntle.Worker` depends on `Application` and `Infrastructure`.
- Use vertical slices for feature implementation.
- Keep controllers/endpoints thin.
- Put business rules in domain/application code.
- Do not create generic repositories for every entity.
- Use EF Core directly for simple persistence work.
- Add specific repositories only when they clarify complex aggregate workflows.

## Backend Structure

- `backend/src/Mabuntle.Api`: HTTP endpoints, auth wiring, middleware, API formatting.
- `backend/src/Mabuntle.Application`: commands, queries, DTOs, validators, interfaces.
- `backend/src/Mabuntle.Domain`: entities, value objects, domain events, business rules.
- `backend/src/Mabuntle.Infrastructure`: EF Core, provider adapters, storage, search, external integrations.
- `backend/src/Mabuntle.Worker`: background job host.
- `backend/tests`: unit and integration tests.

## Frontend Structure

- Public ecommerce pages should be SSR/hybrid friendly.
- Private dashboards can be client-rendered.
- Use Angular Material for standard controls and Tailwind for layout utilities.
- Keep route shells under `src/app`.
- Keep API calls behind Angular services; do not call providers directly from components.

## Commands

```powershell
dotnet restore backend\Mabuntle.sln
dotnet build backend\Mabuntle.sln
dotnet test backend\Mabuntle.sln
cd frontend\mabuntle-web
cmd /c npm install
cmd /c npm run build
cmd /c npm test
```

## Security Rules

- No secrets in source code.
- Do not commit `.env` files.
- Do not call payment, AI, or storage providers from Angular.
- Validate seller ownership on seller endpoints.
- Validate admin roles on admin endpoints.
- Audit sensitive admin, finance, payment, and moderation actions.

## Payment Rules

- The platform must maintain its own ledger.
- Do not rely only on payment provider dashboards.
- Payment webhooks must be idempotent and signature-verified.
- Seller balances and payouts must be derived from internal ledger entries.

## AI Rules

- AI calls must go through the backend.
- Do not allow AI to publish products without seller review and platform validation.
- Persist AI requests, responses, cost metadata, and moderation outcomes when AI features are added.
- Use structured outputs for AI-generated listing data.

## Output Expectations

After each task, report changed areas, verification commands run, and any known gaps. Keep changes scoped to the requested work.
