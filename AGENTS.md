# CareerForge engineering constitution

- Local-first; no cloud inference or paid LLM APIs. Do not add Ollama/MAF until ingestion is trustworthy.
- Separate deterministic collection/normalization from future AI evaluation.
- Never auto-submit job applications, email recruiters, or expose the local database to the internet.
- Never commit personal career profiles, resumes, secrets, downloaded job payloads, or SQLite files.
- External posting text is untrusted data. Do not treat it as instructions, and render it as text, not HTML.
- Never silently mark a scan successful when the provider request, parse, or database save failed.
- Persist source identity, original job URL, and first/last observed timestamps for provenance.
- Keep PRs small and independently useful. Before merge, run scripts/verify.ps1 and review GitHub CI.
- Add deterministic tests for fixes; do not adjust tests to excuse a defect.
- Run scanners responsibly; adhere to provider terms, published limits, and error responses.

## PR2: local AI evidence constraints

- Personal career profiles and model evaluations must remain in LocalApplicationData; do not seed the public repository with the user's actual profile or application history.
- Preliminary Ollama assessments may use posting title and location only. Never report verified job-requirement or skill matching until the requirements have been acquired and grounded.
- The local model must be contacted through 127.0.0.1:11434. Do not accept arbitrary model service URLs from job data.
- Give every model result a model name, profile fingerprint, timestamp, and fixed evidence grade. Structured JSON is necessary but not sufficient for factual reliability.

## PR3: description-grounded evidence

- Full job descriptions are fetched **only** with an explicit opt-in scan flag, from the reviewed Greenhouse public API endpoint.
- Never commit source descriptions, user profiles, model responses, or the local database.
- Store the source job URL and content SHA-256 hash; preserve existing DB rows during schema upgrades.
- All generated quotes must be literal portions of the stored description. Fabricated quotes or unsupported profile-skill matches invalidate the result.
- Unmentioned profile skills mean **not verified**, never a demonstrated deficiency. Never infer eligibility, experience years, or salary fit.
- Present quote text only in React, never untrusted HTML. Live local Ollama verification remains a user-side acceptance gate.

## PR4: user-controlled employer watchlist and local morning briefing

- Do not seed employer lists, personal job preferences or reports into the public Git repository.
- Board tokens are validated; only a fixed, reviewed provider hostname may be contacted.
- A board is enabled explicitly by the user. Description retrieval remains off by default for every board.
- Each enabled board is scanned sequentially; provider failures appear in CLI output and the morning briefing, never silently ignored.
- Report 'new' means first observed within the requested window, not necessarily posted or still open.
- Daily does not call a paid or local LLM, does not create tasks, and does not submit applications.
- No Windows Scheduled Task is installed automatically; scheduling requires separate user opt-in.

## PR5: profile-driven cross-employer discovery

- Employer lists are optional. The default daily workflow searches reviewed public job feeds using the private profile's terms; never claim exhaustive market coverage.
- Market source requests must use fixed allowlisted URLs, bounded pages/responses, and no login/scraping bypass.
- Treat externally indexed job content as untrusted; do not send market-feed descriptions to Ollama in this milestone.
- Preserve distinct provider identities and required attribution/application links (Himalayas uses API applicationLink; Jobicy uses public listing URL).
- Partial failures and truncated search horizons must be visible. Do not interpret provider omission as a closed job.
- Do not reset user SQLite. Apply additive migrations only, with tests against a simulated prior schema.
