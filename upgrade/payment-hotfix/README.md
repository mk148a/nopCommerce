# BNPL incident hotfix — 2026-10-29

The deployed 4.80 plugin's portable PDB identifies the existing 4.80 source worktree; all 70 non-generated core source document hashes match. The beta/5.00 upgrade tree is not the deployment source. This branch starts at `7fe2b098e1`; unrelated dirty source changes were not copied or modified.

## Verified cause and change

A live Affirm attempt used a Romanian customer country and EUR presentment against the configured US merchant country. Stripe rejected Checkout creation because no configured payment method supported the currency. The exception left a `creating` placeholder, and the scheduler later treated its 35 minute local deadline as permission to void and cancel the unpaid order. That downstream note did not describe an Affirm credit decline or customer abandonment.

Affirm now requires the documented domestic US/USD or CA/CAD combination during cart eligibility and order revalidation. Every BNPL adapter also revalidates the cart in `ProcessPaymentAsync`, before nopCommerce saves the order, covering a stale selected method after currency or address changes. GBP/GB private preview is not enabled by any existing setting and remains unavailable.

Stripe HTTP 400 `invalid_request_error` (excluding idempotency errors) is recorded as `create_rejected`; network, 5xx, and indeterminate failures become `create_unknown`. Logs retain structured status/type/code/request correlation without customer inputs. No new key is allocated for unresolved historical/expired creation. Known real sessions must be retrieved and verified before another attempt; only remotely expired, unpaid sessions without an in-flight payment can permit retry. Cleanup reconciles remote paid sessions through the existing finalizer and requires remote expiry proof before cancelling known sessions. Unknown creation and processing/completed sessions are preserved.

Checkout allocation, finalization, and cleanup share one process-local order gate. The cleanup releases it before invoking the finalizer. Existing database attempt/payment CAS guards remain in place.

## Operational precondition and limits

This hotfix requires a single owning IIS application pool worker, `maxProcesses=1`, with `recycling.disallowOverlappingRotation=true`, and no second application/task host using the same store database. The root deployment operator verifies/enforces these conditions, stops the owning pool, waits for its process to exit, installs the complete file set, then starts it. The order gate does not support scale-out or overlapping workers. Introducing another host requires durable order exclusion first.

An unresolved historical creation remains pending for provider/manual reconciliation; it is not automatically reattempted or cancelled. Existing cancelled orders are not reopened, charged or refunded by this patch. No schema migration or SDK/API upgrade is included. No customer checkout or live funds are used for tests.

## Build and validation

Run `Build-Hotfix.ps1 -LiveRoot <verified active physical path>` from PowerShell. It reads existing live binaries as compiler references and writes only the isolated `artifacts` directory; it neither starts nopCommerce nor reads appsettings. The five assemblies target .NET 9, retaining assembly names/version 1.0.0.0 and plugin SystemNames. No Nop.Web/core service/Stripe SDK binary is rebuilt or deployed.

Run `dotnet test PaymentHotfix.Tests.csproj -p:LiveRoot=<verified active physical path> -c Release --logger "trx;LogFileName=hotfix.trx"`. The isolated harness uses live reference binaries, mocks provider interactions, and uses SQLite `:memory:` for actual reservation-store tests. It does not load application configuration. Coverage includes domestic eligibility, actual adapter preflight, definitive/unknown creation errors, unresolved attempts not rotating keys, missed-paid retries, remote state/identity checks, cleanup preservation, and controlled order-gate concurrency/reentrancy. 42/42 passed on the implementation run; independent review is recorded separately.

## Deployment footprint and rollback

`deployment-manifest.json` lists fourteen files: nine DLL targets (the core DLL, four provider DLLs, and the core copy below each provider's `refs` directory) plus five matching primary plugin PDB files. All five core DLL locations receive the same rebuilt core bytes. Update the complete set together while the owning worker is stopped. Matching PDB companions preserve future debug/source provenance; there are no PDB copies in the provider `refs` directories. No configuration, database, theme, view, SDK, Nop.Web, runtime/deps, or unrelated plugin file belongs to this release footprint.

Before deployment, retain the exact fourteen old target files using the central backup/retention mechanism and export the owning pool's previous settings. On failure stop/wait, restore those fourteen files, restore the prior pool settings if reverting the release, then start and verify HTTP and application logs. Deployment/readback and backup details are owned by the root operator.

Research: official Stripe Affirm, Checkout, low-level error, idempotency, and webhook documentation were inspected by the root before the change; the dated 25-source evidence packet remains outside the web root under the central knowledge directory. Research is distinct from mocked tests and live application readback.
