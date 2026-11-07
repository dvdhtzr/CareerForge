# PR 2 — Local-first profile and Ollama metadata triage

## User workflow

The repository remains public, but the user's career profile is **never committed**.
Create it using the interactive CLI:

~~~powershell
dotnet run --project .\src\CareerForge.Worker -- profile init
~~~

The profile is written to the current user's local application data directory
(`%LOCALAPPDATA%\CareerForge\profile.json` on Windows).
Review with `profile show`, or rerun `profile init` to replace after confirmation.

Discover positions via the PR1 scanner, then request local Ollama analysis:

~~~powershell
dotnet run --project .\src\CareerForge.Worker -- greenhouse
ollama pull qwen3.5:9b
dotnet run --project .\src\CareerForge.Worker -- evaluate --model qwen3.5:9b --limit 5
~~~

An Ollama server must already be running on `http://127.0.0.1:11434`.
Only the local Ollama chat API is contacted for model inference; no paid provider is used.
The HTTP payload uses structured-output JSON schema and stream=false.

## Evidence/meaning

**This is a metadata-only evaluator.** The Greenhouse adapter currently retains title,
location, board ID and original posting URL. It does not retain job descriptions, salary
ranges, required skills or benefits. A model does **not** know those requirements.

An evaluation reports a preliminary title-fit category (strong/possible/weak),
a rationale and a required uncertainty statement. The labels describe how promising
a *title* looks, not whether a candidate qualifies. The service assigns a fixed
`metadata-only` evidence grade independently of model output.

Deterministic triage labels explicit title exclusions as excluded, exact target-title +
preferred/remote location matches as candidates, and uncertain others as review.
Unknown geography/title is not treated as rejection. Exclusions are user-configured.

A profile fingerprint is stored with each result, so the UI only shows results
generated against the current profile. The evaluations JSON file also stays in
the user's application-data directory, never in the repository.

## Limitations and next work

- Live Ollama model output is not covered by CI: deterministic mocked HTTP tests
  verify request/response behavior. Execute an actual local run before relying on the output.
- No detailed requirement matching, salary evaluation, commute calculation or AI-generated resumes yet.
- No job descriptions are ingested. Permission-aware description collection and
  evidence-grounded requirement extraction belong to PR3.
- JSON result storage is single-user/single-writer. Do not run concurrent evaluator processes.
- Rerunning evaluate overwrites a job/model/profile combination; no prompt transcript is persisted.
- React API must stay bound to loopback. Read-only endpoints expose assessments to local users.
- A model response can still be inaccurate even if it passes JSON-schema validation.
