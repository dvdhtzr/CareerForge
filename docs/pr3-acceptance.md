# PR3 — Evidence-grounded job descriptions

## Explicit consent and provenance

Greenhouse Job Board API documents `GET /v1/boards/{board_token}/jobs?content=true`.
This publicly accessible endpoint returns full job descriptions. Public API availability alone
is **not blanket permission to redistribute descriptions** or process restricted third-party content.
CareerForge therefore fetches job content only when the operator explicitly opts in per board.

~~~powershell
dotnet run --project .\src\CareerForge.Worker -- scan --with-descriptions greenhouse
~~~

Description content is converted to plain text, bounded to 40,000 characters and stored
only in the user's SQLite database outside Git. HTML scripts/forms/iframes/styles are removed.
Each stored description contains the source job URL, retrieval UTC time and SHA-256 content hash.
Missing descriptions on a subsequent opt-in scan are removed to avoid stale evidence;
a normal (metadata-only) scan leaves existing descriptions unchanged.

## Grounded local Ollama evaluations

~~~powershell
dotnet run --project .\src\CareerForge.Worker -- profile init
dotnet run --project .\src\CareerForge.Worker -- requirements --model qwen3.5:9b --limit 3
~~~

Ollama runs on loopback. Its output includes up to 6 claimed requirements.
Every quote must occur in the actual stored description; otherwise the entire evaluation
is rejected. Any claimed matching skill must be **exactly** on the private profile's
skill list **and** appear in the quoted requirement. A missing skill is **unverified**,
not a demonstrated capability gap. Self-reported skills are not verified past experience.
There is no automated application submission.

Requirement results are locally stored and linked to profile fingerprint + source hash.
A later description/profile change hides stale evaluations. The API exposes **only** verified
short quotes and counts to the loopback dashboard, not full descriptions. The user can open
the original job posting to inspect context.

## Legacy SQLite upgrade

PR1/2 used EF Core `EnsureCreated`, which cannot automatically add tables to older DBs.
`JobDatabaseSchema.EnsureReadyAsync` creates the new description table idempotently
without dropping or replacing historical Jobs/Scans. The upgrade is tested using an existing
database with the new table removed.

## Acceptance gates

- [ ] .NET 10 restore + build + xUnit GREEN on GitHub Actions.
- [ ] React/TypeScript build GREEN.
- [ ] Opt-in URL uses `?content=true`, default scanner does not.
- [ ] Script/style/iframe/form markup is not included in stored plain text.
- [ ] Existing PR1/2 databases upgrade without losing job/scan rows.
- [ ] Repeated scans upsert one description per job and update hash if changed.
- [ ] Fabricated model quotes/skills fail closed, no saved assessment.
- [ ] Dashboard displays only hash-current, profile-current requirement evidence.
- [ ] Windows live opt-in scan and Ollama processing manually verified before treating as production-ready.

## Deferred

Per-company policy registry and provider-specific crawl cadence; more job boards;
salary parsing; semantic requirement-equivalence matching; claim-backed resume generation;
human review queue and applying to jobs. No claim of complete job-market coverage.
