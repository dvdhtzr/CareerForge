# Public market-source policy (PR5)

## Scope

Cross-employer indexed job discovery, not scraping individual company websites.
The user supplies only a private career profile. No employer allowlist is needed.
Published API terms may change and must be re-reviewed before increasing use.

### Himalayas

- Documentation: https://himalayas.app/docs/remote-jobs-api
- Endpoint: `GET https://himalayas.app/jobs/api/search?q=...&country=US&sort=recent&page=N`
- No authentication required; unauthenticated requests share a public rate allowance.
- Maximum two 20-result pages per query, at most four queries derived from the user's profile.
- API results are cached daily. Do not poll more than daily.
- Must use the API's `applicationLink` unchanged for application links, and visibly attribute
  Himalayas. Do not syndicate the data to other aggregators, job boards, Google Jobs or LinkedIn.
- Only ID, company, title, eligible-remote location, application URL, source timestamps
  and expiry time are retained in local SQLite. No job-description/HTML ingestion.
- Keep the exact returned applicationLink. Display clickable Himalayas attribution.
- Skip already expired listings; purge previously stored Himalayas jobs once expiry is reached.
  A job may still close earlier, so the application remains responsible for checking availability.

### Jobicy

- Documentation: https://jobicy.com/jobs-rss-feed
- Endpoint: `GET https://jobicy.com/api/jobs?count=100&geo=usa&tag=...&cursor=...`
- Public, no key required. Public `url` is the Jobicy listing and must be used unchanged.
- At most two 100-result pages per profile term; no more than four terms per run.
- The public API reports a 7-day publication window; absence from a later feed is not closure.
- No commercial API key, paid ATS direct URLs or user accounts.
- Attribute Jobicy visibly, retain public listing link. Do not poll more than hourly (daily
  recommended). No full job-description/HTML ingestion.

## Operational limits and honesty

- Both sources primarily index remote jobs and do **not** offer exhaustive coverage of
  all U.S. employers. No basis exists for claiming comprehensive on-site / hybrid coverage.
- User profile `AllowRemote` must be true to activate these feeds. If false, fail clearly
  instead of silently searching remote-only openings.
- Market results are recorded separately as `(provider, boardToken='market', external ID)`.
  IDs are SHA-256 hashes of provider-supplied stable job IDs. No cross-provider identity
  merging is attempted.
- A failed source is marked PartialFailure; previously imported records remain, but are
  not proof a posting is still active. No retries designed to bypass rate limits.
- This first release is user-triggered; a later scheduler must enforce daily budgets.
- No provider API job descriptions are passed to local LLMs in PR5. Human review of the
  original source remains mandatory.
