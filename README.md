# CareerForge

Local-first career operations: discover real job postings, keep provenance and scan history,
and review opportunities without giving your search or personal profile to a paid AI API.

**PR 1 scope:** a deterministic Greenhouse job-board collector, SQLite persistence, read-only
ASP.NET Core API, CLI worker, React dashboard, and verification tests. **Ollama/MAF are not wired
in yet.** No automated applications, no emailed messages, and no web scraping of login-only sites.

## Prerequisites

- Windows 11, .NET 10 SDK, PowerShell 7 or Windows PowerShell, Node.js 22+ and npm.
- Internet access for the initial NuGet/npm restore and for public Greenhouse feeds.
- Ollama is **not required** for this first milestone.

## Run a first scan

From the solution root:

~~~powershell
dotnet run --project .\src\CareerForge.Worker -- greenhouse
~~~

greenhouse is an example company board token, not a user-specific recommended employer.
Replace/add tokens discovered from real employer Greenhouse careers URLs. For example,
run dotnet run --project .\src\CareerForge.Worker -- greenhouse another-board-token.
The collector uses Greenhouse's documented public GET endpoint:
https://boards-api.greenhouse.io/v1/boards/{board_token}/jobs.
It does not visit arbitrary input URLs. Public board tokens and availability can change.
Failed scans are recorded and yield non-zero worker exit codes; HTTP failures are not successes.

The database is created in %LOCALAPPDATA%\CareerForge\careerforge.db on Windows.
Override this by setting ConnectionStrings__CareerForge to a full SQLite connection string.
Never commit or publicly share this database when personal annotations are added later.

## Run the API and dashboard

In separate PowerShell windows:

~~~powershell
dotnet run --project .\src\CareerForge.Api --launch-profile CareerForge.Api
~~~

~~~powershell
cd web
npm install
npm run dev
~~~

Open http://localhost:5173. Vite proxies /api to the API listening at
http://127.0.0.1:5088. Read-only endpoints include /api/health, /api/jobs?limit=200,
and /api/scans. Keep the API bound to loopback; it has no authentication.

## Verify

~~~powershell
.\scripts\verify.ps1
~~~

If your execution policy blocks unsigned scripts, call:

~~~powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
~~~

You can also run dotnet test CareerForge.slnx and cd web; npm run build independently.

## Safety and known limitations

- Greenhouse GET data is public and does not need a key; public readability is **not** a blanket
  authorization to scrape other sources. Every new adapter gets a source-specific policy review.
- Fixed API hostname, validated board tokens, disabled redirects, download limit, strict HTTPS
  job URLs, and React's default text escaping help reduce untrusted-data risks.
- No job descriptions or resumes are stored in PR1. There is no autonomous LLM operation.
- A job not seen in one scan is **not** automatically marked closed. LastSeenAtUtc is evidence
  of observation, not evidence that a posting is presently open.
- SQLite schema uses EnsureCreated() for this first milestone only; formal EF migrations and
  concurrency/locking will be added before schema changes or scheduled overlapping scans.
- Duplicate identity is (provider, board token, external posting ID), not similarity by title.
  Cross-board syndication and duplicate canonicalization are future work.
- The dashboard is a reviewable inventory, not yet an AI-ranked shortlist. Salary/location
  matching, the second brain, and Ollama evaluation belong to the next milestone.

See docs/architecture.md and AGENTS.md.

## Phase 2 — private profile and local Ollama title triage

Create a profile through the terminal's guided prompts; **no JSON file editing is required**:

~~~powershell
dotnet run --project .\\src\\CareerForge.Worker -- profile init
~~~

The profile is written outside your Git checkout to `%LOCALAPPDATA%\\CareerForge\\profile.json`.
To review it, run `dotnet run --project .\\src\\CareerForge.Worker -- profile show`.
Do not publish or share your profile/assessments JSON files.

After a normal Greenhouse scan and with local Ollama running:

~~~powershell
ollama pull qwen3.5:9b
dotnet run --project .\\src\\CareerForge.Worker -- evaluate --model qwen3.5:9b --limit 5
~~~

Refresh the React dashboard to see title alignment assessments and uncertainty statements.
The HTTP API exposes `GET /api/profile/status` and `GET /api/evaluations`, both read-only.
**Model scores are based on role title/location only.** Requirements, salary and qualification fit
are not yet known; do not use these assessments as proof of suitability. Ollama stays on local
loopback and will not send personal data to cloud inference providers.

See [Phase 2 design and limitations](docs/phase2.md).
