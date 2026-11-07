# Source policy — milestone 1

## Greenhouse public Job Board GET interface

- Provider: Greenhouse Job Board API (boards-api.greenhouse.io).
- Documentation: https://docs.greenhouse.io/job-board.html
- Reviewed interface: public GET /v1/boards/{board_token}/jobs. The documentation
  explicitly states public GET endpoints do not require authentication.
- Intended acquisition: posting ID, title, location, original HTTPS URL, and source update time.
  No job description or application questions are ingested for this milestone.
- Persistence: normalized posting fields and first/last observation timestamps in local SQLite.
- Network restrictions: board-token allowlist character validation, fixed provider host,
  no redirects, 30-second timeout, 20 MB response limit.
- Rate/usage: scans are initiated manually, sequentially; automated cadence and rate limits
  must be added before scheduling frequent requests. Do not bypass access restrictions.
- Exclusions: application POST endpoints, user accounts, login-only pages, third-party scraping,
  unrestricted browsing, and automated applications.

**Important:** Public access to this documented API is not a blanket license to redistribute
job descriptions or ingest other websites into models. Additional providers require individual
policy and permission reviews.

## PR3: description access is explicitly opt-in

Greenhouse's public GET `/v1/boards/{board_token}/jobs?content=true` is documented at
https://docs.greenhouse.io/job-board.html . The optional content flag returns full public job
post descriptions. That API is intended for public job board / careers site integrations.

- User must choose `scan --with-descriptions <board>`; default scans fetch metadata only.
- Only local use: descriptions stay in the user's SQLite database, are not committed to Git,
  and are never published on a remote dashboard.
- We remove HTML scripting/form/style elements, cap input size, and save plain text with
  source URL, retrieval time and content hash.
- Only short quotes verified against source text are exposed to the local dashboard.
- Public API access does not itself grant a universal copyright license for redistribution or
  permission to ignore individual employers' terms. Employer-specific restrictions must be
  reviewed before enabling automated bulk processing. The operator remains responsible for
  choosing authorized boards and responsible request rates.
