# API implementation roadmap

This is planned work, not a description of implemented endpoints. Suggested routes and fields below must be finalized before implementation and recorded in `mobile-api-contract.md`. Keep the current ASP.NET Core/PostgreSQL stack and service boundaries; do not add layers unless they own meaningful behavior.

The current API supports login and refresh, food lookup, and meal creation/listing. The next goal is complete, dependable user workflows. Implement the stages in order, using focused commits and tests. Existing accounts and meals must remain usable after every migration.

## 1. Registration, current user, logout, and authentication throttling

### Decisions before implementation

- Decide whether registration requires an email address. The current user model has no email; email-based recovery and verification require schema and delivery work.
- Define username normalization and case-insensitive uniqueness. Decide whether usernames can change; use an immutable user ID for authorization and ownership rather than relying on a mutable username.
- Define password policy, registration response, token lifetimes, and whether logout affects one session or all sessions.

### Backend work

- Add a registration request DTO and endpoint, for example `POST /auth/register`. Validate bounded inputs, normalize identity fields, hash passwords using the existing password hasher, and never return password hashes.
- Enforce normalized username uniqueness with a database unique constraint, not only a pre-insert check. Handle concurrent duplicate registrations with a documented error response. Audit and resolve existing collisions before applying the constraint.
- Add `GET /users/me`, returning a deliberately limited profile DTO with a stable user identifier. Use the authenticated identity, not a user ID selected by the client.
- Add an authenticated logout endpoint and revoke the intended refresh session. Define ownership checks and repeat-call behavior. If supporting logout everywhere, revoke all refresh sessions for the authenticated user.
- Document that revoking refresh tokens does not immediately invalidate already issued access tokens. Keep their lifetime bounded or deliberately implement immediate revocation if required.
- Add separate throttling policies for login, registration, and refresh. Define trusted proxy handling, limiter partitioning, and `429` responses with retry guidance. Avoid unlimited per-username/IP limiter state and account-disclosure errors.
- Do not expose account status or credentials through logs. Check password rehash-on-success behavior when the hasher reports that an upgrade is needed.

### Mobile work

- Add registration and current-user loading states and handle field-validation, duplicate-registration, and throttling responses according to the finalized contract.
- Store tokens in platform secure storage. Serialize refresh attempts so concurrent requests do not independently rotate the same refresh token; atomically replace the saved token pair.
- On logout, call the server revocation endpoint and clear local credentials and account-specific cached data. Define behavior when logout happens offline.
- Treat failed refresh as session expiration and return to authentication without an endless retry loop.

### Acceptance checks

- Valid registration and login succeed; passwords and hashes never appear in responses or logs.
- Concurrent registrations for the same normalized username create exactly one account.
- Anonymous requests cannot access the current-user endpoint. User A cannot revoke User B's session.
- A revoked refresh token cannot refresh again; repeat logout follows the documented behavior.
- Authentication throttling triggers predictably, and valid traffic still works after the limit resets.

## 2. Meal IDs, editing, deletion, dates, and pagination

### Decisions before implementation

- Define editable meal fields, maximum foods per meal, and whether updates replace the entire meal or modify individual foods.
- Add an occurrence timestamp such as `occurredAt` separately from the server-managed creation timestamp. Define UTC storage, accepted offsets, and how a mobile user's local day maps to query boundaries.
- Choose cursor or offset pagination, maximum page size, and a deterministic sort order with a unique ID tie-breaker.

### Backend work

- Include a stable meal ID in meal responses. Preserve stored food snapshots rather than silently replacing historical values with new provider results.
- Add owned-resource retrieval, update, and deletion, for example `GET`, `PUT`, and `DELETE /meals/{id}`. Perform ownership filtering in repository queries and define consistent not-found behavior for missing or inaccessible resources.
- Validate updates as rigorously as creation. Save meal and food changes transactionally so failures cannot leave a partially updated meal.
- Introduce `occurredAt` with a migration that safely backfills existing meals from their creation time. Keep creation timestamps immutable.
- Add bounded date-range filtering and pagination to meal listing. Define inclusive/exclusive boundaries and response metadata or next-page cursors.
- Define lost-update behavior. If using optimistic concurrency, return a version or ETag and reject stale updates with a documented conflict/precondition response.
- Add database indexes based on owner, date, and ordering queries, and check the generated SQL against realistic history sizes.

### Mobile work

- Use meal IDs for navigation and local state updates; never identify meals only by timestamps or array positions.
- Add edit/delete flows, occurrence-date selection, paged history loading, and empty/error/loading states.
- Refresh or invalidate affected cached pages after changes. Handle stale-edit conflicts without silently overwriting another device's changes.
- Coordinate any change from a bare list to a paginated response envelope; use a compatibility period or versioned route when existing clients need it.

### Acceptance checks

- User A cannot retrieve, edit, or delete User B's meal, even when its ID is known.
- Existing meals survive migration and have a valid occurrence time.
- Updates are atomic, invalid inputs leave data unchanged, and deletion behavior is documented.
- Date filters work across offsets and daylight-saving boundaries. Pagination is deterministic when timestamps match.
- Page limits are enforced, and historical nutrition snapshots remain unchanged by provider updates.

## 3. Retry-safe creation and mobile contract tests

### Decisions before implementation

- Define an idempotency mechanism: a request header such as `Idempotency-Key` or a client-generated creation ID.
- Define key scope, retention duration, payload comparison, and responses for reused keys with different payloads or work still in progress.
- Define the offline queue's retry window so it is compatible with server deduplication retention.

### Backend work

- Persist deduplication state in PostgreSQL, scoped to the authenticated user and operation. Enforce uniqueness in the database so retries across processes or instances cannot create duplicates.
- Make meal creation and its deduplication result atomic. A request that loses its connection after commit must be safely replayable without creating a second meal.
- Store or reconstruct the same logical creation result on replay. Reject a key reused with a different normalized payload; do not silently treat it as a new operation.
- Bound key and payload sizes, define retention/cleanup, and avoid storing credentials or unnecessary sensitive request data.
- Standardize application errors using a consistent Problem Details contract with stable machine-readable identifiers, field errors where relevant, and a trace identifier that does not expose internals.
- Add HTTP-level tests for login, refresh, lookup, creation, history, edit, delete, and logout. Use actual PostgreSQL tests for uniqueness, concurrency, and transaction guarantees.

### Mobile work

- Generate one creation key when the user starts a logical save and reuse it for every retry, including retries after app restart. Generate a new key for a genuinely new meal.
- Persist pending saves safely and expose pending/failed/saved states. Do not retry validation failures automatically; use bounded backoff for transient failures and honor server retry guidance.
- Handle `401`, `404`, validation errors, conflicts, throttling, and provider outages distinctly instead of displaying one generic failure for everything.
- Test session expiration, simultaneous refresh attempts, offline saves, and lost-response retries against the documented API contract.

### Acceptance checks

- Concurrent identical creation retries persist one meal and return the same logical result.
- Reusing a key with another payload is rejected. Keys belonging to different users do not collide.
- A database failure cannot commit a meal without the associated deduplication state.
- Restarting the API or mobile app does not defeat deduplication within the documented retention window.
- Automated tests exercise the same request/response shapes used by the mobile client.

## 4. Recovery, account deletion, and operational launch checks

### Backend work: account recovery

- Implement recovery only after choosing and verifying an account recovery channel. If using email, add verification state, delivery configuration, and operational failure handling.
- Add recovery-request and reset endpoints with generic account-existence responses. Generate cryptographically random, short-lived, single-use tokens; store token fingerprints rather than plaintext tokens.
- Enforce atomic single use, expiration, bounded inputs, and throttling. Never log reset tokens or construct reset URLs from untrusted host input.
- After a reset, invalidate affected refresh sessions and define handling of still-valid access tokens. Add authenticated password change with current-password verification or recent reauthentication.

### Backend work: account deletion and operations

- Define deletion semantics and any retention obligations before adding an authenticated deletion endpoint. Require appropriate reauthentication for destructive account actions.
- Delete or anonymize associated meals, credentials, recovery records, and sessions according to the policy. Review foreign keys and transactional behavior; prevent deleted users from retaining authorized access through existing tokens.
- Implement useful error monitoring and metrics: request latency, error rate, database failures, food-provider failures/timeouts, and refresh failures. Redact credentials and personal data; avoid raw search text in routine telemetry unless deliberately justified.
- Configure readiness/liveness checks, database backups, and a tested restore procedure. Define who receives alerts and how deployment rollback works with schema changes.
- Run cross-user authorization tests, authentication-abuse tests, and realistic load tests before public launch. Verify migrations against a production-like database without using real production credentials or data.
- Review the .NET support lifecycle and schedule coordinated SDK, runtime, package, container, and CI upgrades.

### Mobile work

- Add recovery and password-reset flows with expiry/error handling and validated app-link behavior.
- Add deletion confirmation and required reauthentication. Clear credentials and local account data after successful deletion.
- Present recoverable server failures without losing unsaved work, and attach a support-safe trace identifier when appropriate.

### Acceptance checks

- Recovery responses do not disclose account existence; expired or reused tokens fail, including concurrent reuse.
- Password reset, password change, and deletion produce the documented session-revocation behavior.
- Account deletion does not leave orphaned sensitive records or authorized sessions.
- A backup has actually been restored into an isolated environment and its contents verified.
- Monitoring detects simulated database/provider failures without leaking secrets.

## 5. Food data quality and provider failure semantics

### Decisions before implementation

- Define the canonical nutrition basis and units: per-100-gram source values versus serving-scaled response values. Account for provider-specific units before aggregation.
- Decide what source attribution, measurement basis, and confidence information the client should show. Consensus confidence is an internal agreement heuristic, not proof of accuracy.
- Define separate results for a genuine no-match, unavailable providers, and a response served from older cached data.

### Backend work

- Audit provider mappings using representative recorded fixtures. Add finite/nonnegative numerical checks where appropriate; current candidate upper-bound heuristics are not complete numerical validation.
- Make quantity and unit semantics explicit in response DTOs and saved-meal inputs. Ensure repeated scaling never compounds and cached source values remain independent of serving size.
- Preserve provenance and quality information needed to explain a result. Avoid treating missing data as a measured zero when that distinction matters.
- Propagate enough provider outcome information to distinguish no matches from upstream failures. Define status codes, stale-result indicators, and retry guidance without exposing raw provider internals.
- Support manual food entry and corrections using validated meal snapshots. Decide separately whether reusable custom foods need their own owned-resource endpoints; do not build a shared catalog without moderation/ownership decisions.
- Measure lookup success, latency, and correction patterns with privacy-conscious telemetry before changing consensus formulas or adding providers.
- Apply bounded food-lookup throttling, provider budgets, and cancellation where useful. Assess process-wide upstream concurrency limits separately from the existing per-search metadata limit.

### Mobile work

- Clearly represent serving units, missing information, stale data, and unavailability according to the contract.
- Provide manual entry or correction when lookup results are missing or unsuitable. Save the user's confirmed snapshot rather than changing it silently afterward.
- Preserve entered quantities across retries and avoid scaling already scaled response values a second time.

### Acceptance checks

- Provider fixtures cover missing fields, different units, invalid numbers, partial nutrition, and conflicting identities.
- Different serving quantities reuse source caches but return correctly scaled independent DTOs.
- Provider outages are distinguishable from valid empty searches, including partial-failure cases.
- Manual corrections persist through history retrieval and do not mutate shared lookup/cache entries.

## Completion checklist for every stage

- Finalize routes, DTOs, error codes, authorization, and compatibility behavior before coordinating mobile changes.
- Keep controllers focused on HTTP behavior, services on use cases, repositories on persistence, and existing food supervisors/validators on their established responsibilities.
- Add migrations only when needed; test backfill, existing-data compatibility, and rollback/deployment implications.
- Add focused unit tests and HTTP contract tests; add PostgreSQL coverage for database-specific guarantees.
- Update XML comments, `mobile-api-contract.md`, and operational instructions alongside implementation.
- Verify the solution build, regression suite, and Docker startup. Create focused local commits; push only when explicitly requested.

## References

- [Current mobile API contract](mobile-api-contract.md)
- [Service architecture](service-architecture.md)
- [Operations and local setup](operations.md)
- [OWASP authentication guidance](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [OWASP password recovery guidance](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html)
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0)
