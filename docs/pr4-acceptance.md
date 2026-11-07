# PR4 — Employer watchlist and morning briefing

## Goal

Make the collector useful on a daily basis without hard-coding employers or personal
preferences into a public repository. No cloud AI, no automated applications, and no paid keys.

## Local watchlist

The CLI maintains `%LOCALAPPDATA%\CareerForge\sources.json` using atomic file replacements:

- `sources add <board-token> [company-name]` (enabled, metadata-only by default)
- `sources list`
- `sources disable <board-token>` / `sources enable <board-token>`
- `sources descriptions <board-token> on|off` (explicit opt-in; review employer terms)
- `sources remove <board-token>`

The watchlist rejects duplicate/invalid tokens and limits itself to 75 boards. It never
fetches employer data during the `sources` commands. No repository file edits are necessary.

## One-command scan / briefing

- `scan watchlist` scans enabled boards sequentially, without cloud inference.
- `report --days 7` reads the local SQLite database only, classifies observed jobs
  using the locally saved career profile, and writes `morning-briefing.txt`.
- `daily --days 7` scans followed by report; a failed feed does not prevent a report
  from being generated or other feeds from running, and the process exits nonzero.
- A small inter-board delay reduces unnecessary burst traffic.
- Never equate first-seen timestamps with the employer's posting dates or an opening's
  continued availability. Human review always occurs at the original posting URL.
- The React dashboard shows source health and recent candidate/review observations.
- The API's `/api/sources` and `/api/briefing` endpoints are read-only and local-only.

## Acceptance

- [ ] Build/test/TypeScript CI all green.
- [ ] Watchlist add/list/disable/remove works; no private content enters Git.
- [ ] No source is scanned unless explicitly configured and enabled.
- [ ] Descriptions default off, require per-board opt-in.
- [ ] One bad board is reported and does not suppress scans of other boards.
- [ ] Deterministic report counts exclude cross-board/unwatched rows.
- [ ] Newly observed means first-seen in window, not last-seen.
- [ ] Missing profile is unclassified, not a rejection.
- [ ] Existing SQLite data preserved; local live feed scan must still be checked.
- [ ] Local Ollama pipeline can be evaluated separately; this command makes no model calls.

## Not included

- No scheduled Windows task installed, and no background process promised.
- No new job providers, unsupported scraping, or application submission.
- No guarantee a posting remains available.
- No automatic creation of private profile data from public account details.
