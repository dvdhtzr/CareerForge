# Architecture — foundation milestone

~~~text
Greenhouse public JSON API
        |
        v
GreenhouseJobClient -- fixed host, bounded response, JSON validation
        |
        v
JobNormalizer -- trim/validate untrusted structured fields
        |
        v
JobScanService -- upsert by (provider, board, external id), persist scan diagnostics
        |
        v
SQLite -- %LOCALAPPDATA%/CareerForge/careerforge.db
        |
        v
ASP.NET Core -- GET /api/jobs, /api/scans, /api/health -- loopback only
        |
        v
React/Vite -- read-only listing and scan diagnostics
~~~

The CLI worker is the **only scan trigger** in milestone 1; HTTP endpoints cannot start
network requests or change stored data. A future background scheduler must guard against
concurrent scans and respect rate limits. The database does not store job descriptions yet.

## Next milestones

1. User-controlled career profile and salary/region constraints, explicitly kept private.
2. Deterministic title/location filter with reason codes and fixture-based accuracy tests.
3. Ollama evaluation on a small filtered subset, preserving input evidence and schema validation.
4. React decision workflow: shortlist, reject, and manually track applications.
5. More source adapters only after review of published interfaces and permitted uses.

## Operational semantics

- Succeeded: HTTP succeeded, response parsed, database changes committed.
- Failed: source/network/parse error; no partial posting writes are retained.
- Found: total provider entries; Skipped: invalid or duplicate IDs in that response.
- Added: newly observed posting identities; Updated: identities already known and refreshed.
- No inference of job closure is made from a missing row in later snapshots.

## Phase 2 enrichment (local-only)

~~~text
Interactive CLI profile init -> LocalApplicationData/profile.json (private)
                                       |
SQLite normalized job metadata -> deterministic triage
                                       |
                               Ollama /api/chat on 127.0.0.1
                              (JSON-schema metadata-only fit)
                                       |
                           LocalApplicationData/assessments.json
                                       |
                          Read-only API -> React fit details
~~~

This is preliminary title/location alignment, not requirements-grounded matching.
An explicit evidence-grade field prevents presenting it as validated suitability.
