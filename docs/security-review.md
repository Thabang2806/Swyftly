# Security Review

Last reviewed: 2026-05-26

## Critical Issues

None identified in the focused review.

## High Issues

None currently open after the 2026-05-19 security remediation pass.

Resolved high issues:

- Production defaults no longer include a usable database password or JWT signing key in `backend/src/Mabuntle.Api/appsettings.json`. Production startup now fails if `Jwt:SigningKey` or `ConnectionStrings:DefaultConnection` are missing, weak, or known placeholder values, rejects `PaymentProvider:ProviderName=Fake` and `CarrierProvider:ProviderName=Fake`, and requires complete HTTPS PayFast configuration with remote ITN validation when PayFast is selected.
- Login now uses Identity lockout state with explicit failed-attempt tracking and temporary account lockout after repeated failed password attempts.
- Anonymous ad impressions now have a dedicated rate-limit policy and API-side fingerprint dedupe when `AnonymousVisitorId` is not supplied.
- Payment webhooks now have a dedicated rate-limit policy, require JSON content, and reject payloads larger than 64 KB before reading/parsing.
- Refresh tokens are now held in HttpOnly secure cookies, refresh/logout require a CSRF cookie/header pair, Angular keeps access tokens in memory only, the Angular bearer interceptor is restricted to the configured Mabuntle API origin, and production startup validates auth-cookie Secure/SameSite/path/domain settings.
- Refund and payout money movement now uses finance-specific read/operate/approve policies and dual-control checks, including for `SuperAdmin`.
- Payment webhook payloads are normalized to sanitized JSON before persistence; signatures, tokens, secrets, passwords, passphrases, card fields, and CVV/CVC values are redacted. Expired stored raw webhook payload JSON is redacted by the worker according to the configured retention window while event metadata is preserved.
- Verified sellers can no longer directly overwrite an approved payout provider reference. Payout-profile changes now use a finance-reviewed request workflow with audit logs, dual-control approval/rejection, and payout-processing blocks while a change is pending.
- Seller verification evidence files are private API-managed uploads rather than public media URLs. Seller upload/remove and admin download actions are ownership/role checked, content-type and signature validated, scanner checked, and audited.

## Medium Issues

- Payment webhook payload encryption-at-rest posture remains an operational production decision. Application-level redaction and retention cleanup are implemented.

## Low Issues

- None currently open from this review.

Resolved medium issues:

- Refresh-token replay now revokes the active replacement chain in that token family.
- Public storefront and public catalog reads now require a published storefront and verified seller.
- Finance actions now use `FinanceRead`, `FinanceOperate`, and `FinanceApprove`, and money movement requires separate actors.
- Angular bearer-token attachment is restricted by parsed URL origin and configured API base path, including protection against deceptive host-prefix URLs.
- Auth-cookie path/domain/SameSite/Secure settings are centrally configured through `AuthCookies` and covered by production configuration validation plus integration tests for emitted refresh/CSRF cookie attributes.
- Payment webhook payload retention cleanup redacts expired raw payload JSON and records `raw_payload_redacted_at_utc`, while preserving event metadata for reconciliation.

## Suggested Remediation Tasks

- Define storage-level encryption expectations for production database backups and managed PostgreSQL storage.
- Reconfirm the final `AuthCookies:Domain` value when production frontend and API hostnames are finalized.

## Tests To Add

- Add deployment smoke checks for the final production frontend/API hostnames once DNS, TLS, and cookie domain are known.
