# Payment hotfix decisions — 2026-10-29

1. Patch the proven 4.80 production source; do not deploy beta upgrade assemblies. Preserve all unrelated dirty worktree files.
2. Reject the documented incompatible Affirm domestic country/currency combination before order insertion through actual payment-adapter preflight; retain Stripe as final credit/account authority.
3. Persist definitive request rejection separately from unknown provider outcome. Unknown attempts never acquire a new key or cancel automatically just because a local clock expires.
4. Retrieve and validate every known previous provider session before permitting another attempt. Cleanup requires remote expiry/unpaid proof; paid provider state is reconciled through the existing finalizer.
5. Share one order gate across checkout/finalization/cleanup; enforce no-overlap single-worker IIS operation for this narrow hotfix. Distributed deployment is unsupported until durable order exclusion is implemented. Root chose this operational precondition rather than adding payment/database infrastructure during the incident.
6. Build only five plugin assemblies with read-only references to the exact live binaries. Deploy nine DLL paths atomically with the owning worker stopped, including four duplicate core reference copies; keep the existing Stripe SDK and application binaries.
7. Tests use mocks and SQLite in memory, never production configuration, provider transactions, or customer records. Reports/logs/artifacts remain outside the served IIS paths.
