# Payment hotfix state — 2026-10-29

Scope: nopCommerce 4.80 BNPL eligibility and indeterminate-creation/cancellation safety. Separate from the unreleased 5.00 Beta upgrade.

- Source mapping: all 70 non-generated live core PDB source hashes matched the original 4.80 source; isolated worktree starts at `7fe2b098e1`.
- Implementation: complete; five plugin builds passed against verified live reference assemblies, zero warnings/errors on final build.
- Validation: 42/42 isolated regression tests passed, including SQLite reservation tests and controlled shared-gate tests. Independent reviewer accepted 42/42 under the deployment precondition.
- Release precondition: owning pool has exactly one worker, overlapping rotation disabled, and no alternate live database application host. Root owns enforcement and readback.
- Deployment footprint: nine DLL targets plus five matching primary PDB companions in `payment-hotfix/deployment-manifest.json`. Schema, runtime, SDK, theme, and Nop.Web changes: none.
- Push/live deployment: implementation commits pushed and remote SHA verified before deployment. Root deployed the 14-file manifest at 2026-10-29 11:36 UTC; independent hashes, loaded modules, one-worker/no-overlap policy and public HTTP readback passed. See payment-hotfix/LIVE_READBACK.md. Real provider payment/settlement remains unverified; no customer transaction was made.
- Historical cancelled orders: no automatic reopening, charging, refunding or customer communication.

Build/test and recovery instructions: [payment-hotfix/README.md](payment-hotfix/README.md).
