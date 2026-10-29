# BNPL hotfix live readback — 2026-10-29

Implementation commits: 3f3ded625a and a9e934f48b on fix/bnpl-indeterminate-session-20261029.
Before deployment, git ls-remote independently returned a9e934f48bc347eecd8b38db2f50c8b0cf75f262, equal to local HEAD. Branch push completed before live file changes.

Deployment: UTC11:36:24.737 through11:36:27.718 (Türkiye14:36). Exact footprint14 files =9DLL+5matchingPDB, verified against deployment-manifest.json. No application/database migration, SDK upgrade or other plugin replacement.

Target: actual active IIS physical path D:\hood-localization-live-20260821-0900-bnpl-stripe-country-fix, owning app pool hoodarcheryshop.com.
Operational constraint applied explicitly: maxProcesses1, recycling.disallowOverlappingRotation=true. The previous value false is retained for rollback. All IIS app configurations were compared to the active database identity in memory: only the owning application matched. No Windows service/task action referenced the live root; observed dotnet processes did not identify a live Nop.Web host. This is a single-host conditional release, not proof of distributed safety.

Method: backup and hash-check14existingfiles, stop pool, check unfiltered worker inventory and wait oldPIDs trulyexit, set nooverlap, copy allowlisted files, hashcheck, start pool. The first attempt aborted beforefilemutation because filtered appcmd no-match returned a nonzero code; prior policy was restored and pool restarted. The fixed, independently reviewed script used full worker inventory and PIDexit validation and reused the same verified backup.

Rollback: D:\CodexData\CodexBackups\bnpl-hotfix-20261029-113458. Central retention registered for14days (expected cleanup2026-11-12). Exact14files, pool-policy.json and manifest.json retained; backup hashes match oldmanifest hashes.
Recovery script: D:\CodexData\workspaces\payment-audit-20261029\Restore-BnplHotfix.ps1 -BackupPath <above path>. It validates allowed targets and hashes, stop/waits, restores files and previouspoolpolicy, starts and verifies restoredhashes. Recovery execution was not needed and was not run.

Validation:
- Five compatible plugin builds: PASS,0warnings/0errors.
- Isolated meaningful regression tests:42/42PASS, independently repeated42/42PASS. Includes realSQLite reservation store, definitive400vs5xx/network, country/currencycart+order+adapter, missedpaidretry, identitychecks, sharedgateconcurrency and finalizerreentry.
- Independent live readback UTC11:37:18:14/14hashmatch, poolStarted/max1/nooverlaptrue/oneWP43092.
- Worker modules show actual core and AffirmDLL from the liveplugin paths. LoadedcoreSHA2569D4E533933F69E52BC9D49FFA7B64FF25301CDD4331A846269FC02B2A76356A1.
- PublicHTTP/en/,/ro/,/en/cart,/ro/cart:all200 UTC11:36:50–51.
- No newBNPL payment/DIbinding failure found in postdeployment logreadback. Othererrorentries were404/notfound; causaloriginunknown.
- Order870unchangedcancelled/voided/unpaid; no newnotes, orders orBNPLactions during verificationwindow.

Limits:
No real customer transaction or Stripe testpayment/settlement was executed. Provider lifecycle tests used controlled mocks and databaseinmemoryfixtures; actual moduleloading/hash/HTTP/startup observations are separate evidence. Historicpaid/cancelled refund exceptions require provider financial reconciliation; they were not automatically retried.
The source gate is processlocal. Introducing anotherworker/host requires durableorderexclusion before doing so.

Official IIS policy references (retrieved2026-10-29):
- https://learn.microsoft.com/en-us/iis/configuration/system.applicationhost/applicationpools/add/recycling/
- https://learn.microsoft.com/en-us/iis/configuration/system.applicationhost/applicationpools/add/processmodel/
Microsoft documents nooverlap for applicationcode that does not support multipleworkers. This constraint was researched before applying it; workerreadback is separate from documentation.

Detailed read-only incident/otherpayment evidence and acquisition report remain nonpublic under D:\CodexData\workspaces\payment-audit-20261029\evidence and REKLAM-KAYNAK-INCELEMESI.md.

