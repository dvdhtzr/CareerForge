# PR 1 — Foundation / Greenhouse Discovery

## Delivered

- .NET 10 solution: Core, Infrastructure, API, CLI Worker, xUnit tests
- Public Greenhouse feed adapter with fixed hostname, token validation, bounded response
- Normalization, HTTPS URL validation, stable identity keys, SQLite unique index
- Durable scan results and explicit error reporting; worker nonzero exit on failure
- Read-only API: health, jobs, recent scans
- Local React/Vite UI: searchable posting list and scan status
- GitHub CI and PowerShell verification
- Git hygiene, source-policy record, local-first engineering constitution

## Must verify on a .NET 10 / Node 22 developer machine

1. Run powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
2. CLI scan for a real, currently active Greenhouse board token.
3. Run API and web dev server; inspect the scan and one original job link.
4. Repeat the same scan: Added=0 and no duplicate rows for an unchanged board.
5. Try a nonexistent but valid board token; the worker exits nonzero and records Failed.
6. Check that local DB files are ignored by Git.

## Known deferred features

- Ollama/MAF job-fit evaluation, career profile, score provenance and review gates
- Salary/location filtering, additional sources, scheduled/nightly scans
- Multi-process scan locking, EF migrations, cross-board duplicate resolution
- Authentication (API remains loopback-only)

## Verification caveat

Initial authoring environment did not have .NET SDK or network package restore. Source,
configuration and TypeScript syntax were checked; .NET build/tests and real feed integration
remain **unverified** until run locally or by GitHub CI. Do not merge without GREEN.
