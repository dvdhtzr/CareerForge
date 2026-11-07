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
