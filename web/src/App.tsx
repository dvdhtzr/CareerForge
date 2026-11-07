import { useEffect, useMemo, useState } from 'react'

type Job = {
  id: string
  boardToken: string
  provider: string
  company: string
  title: string
  location: string
  jobUrl: string
  firstSeenAtUtc: string
  lastSeenAtUtc: string
  hasDescription: boolean
}

type Evaluation = {
  jobId: string
  model: string
  fit: string
  rationale: string
  uncertainty: string
  evidenceLevel: string
  evaluatedAtUtc: string
}

type RequirementReview = {
  jobId: string
  model: string
  evidenceLevel: string
  evaluatedAtUtc: string
  listedSkillCount: number
  claims: { quote: string; profileSkill: string | null }[]
}

type BriefingSource = {
  boardToken: string
  provider: string
  displayName: string
  enabled: boolean
  includeDescriptions: boolean
  lastStatus: string
  lastAttemptUtc: string | null
  lastAdded: number | null
  lastError: string | null
}

type BriefingJob = {
  id: string
  boardToken: string
  provider: string
  company: string
  title: string
  location: string
  jobUrl: string
  firstSeenAtUtc: string
  lastSeenAtUtc: string
  triage: string
  reasonCodes: string[]
}

type Briefing = {
  generatedAtUtc: string
  windowDays: number
  profileConfigured: boolean
  recentCount: number
  candidateCount: number
  reviewCount: number
  excludedCount: number
  unclassifiedCount: number
  omittedCount: number
  jobs: BriefingJob[]
  sources: BriefingSource[]
}

type ProfileStatus = { configured: boolean }

type Scan = {
  id: string
  boardToken: string
  status: string
  found: number
  added: number
  updated: number
  skipped: number
  error: string | null
  startedAtUtc: string
}

const fmt = (value: string) => new Date(value).toLocaleString()

export default function App() {
  const [jobs, setJobs] = useState<Job[]>([])
  const [evaluations, setEvaluations] = useState<Evaluation[]>([])
  const [requirementReviews, setRequirementReviews] = useState<RequirementReview[]>([])
  const [profileConfigured, setProfileConfigured] = useState(false)
  const [scans, setScans] = useState<Scan[]>([])
  const [briefing, setBriefing] = useState<Briefing | null>(null)
  const [query, setQuery] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  async function reload() {
    setLoading(true)
    try {
      const [jobsResponse, scansResponse, evaluationsResponse, profileResponse, reviewsResponse, briefingResponse] = await Promise.all([
        fetch('/api/jobs?limit=500'), fetch('/api/scans'), fetch('/api/evaluations'),
        fetch('/api/profile/status'), fetch('/api/requirement-reviews'),
        fetch('/api/briefing?days=7'),
      ])
      if (!jobsResponse.ok || !scansResponse.ok || !evaluationsResponse.ok || !profileResponse.ok || !reviewsResponse.ok || !briefingResponse.ok)
        throw new Error('API request failed')
      const [jobData, scanData, evaluationData, profileData, reviewData, briefingData]: [Job[], Scan[], Evaluation[], ProfileStatus, RequirementReview[], Briefing] =
        await Promise.all([jobsResponse.json(), scansResponse.json(), evaluationsResponse.json(), profileResponse.json(), reviewsResponse.json(), briefingResponse.json()])
      setJobs(jobData)
      setScans(scanData)
      setEvaluations(evaluationData)
      setRequirementReviews(reviewData)
      setBriefing(briefingData)
      setProfileConfigured(profileData.configured)
      setError(null)
    } catch {
      setError('Cannot reach the local CareerForge API. Start the .NET API and refresh.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void reload() }, [])
  const filtered = useMemo(() => jobs.filter(j =>
    (j.title + ' ' + j.company + ' ' + j.provider + ' ' + j.location + ' ' + j.boardToken).toLowerCase().includes(query.toLowerCase()),
  ), [jobs, query])
  const lastScan = scans[0]
  const evaluationByJob = useMemo(() => new Map(evaluations.map(e => [e.jobId, e])), [evaluations])
  const requirementsByJob = useMemo(() => new Map(requirementReviews.map(r => [r.jobId, r])), [requirementReviews])

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-mark">CF<span>◆</span></div>
        <div className="sidebar-caption">CAREER OPERATIONS</div>
        <nav>
          <div className="nav-active">◈ &nbsp; Opportunity Radar</div>
          <div className="nav-muted">◇ &nbsp; Career Profile <span>{profileConfigured ? 'LOCAL' : 'SETUP'}</span></div>
          <div className="nav-muted">◇ &nbsp; AI Evaluations <span>LOCAL</span></div>
          <div className="nav-muted">◇ &nbsp; Applications <span>SOON</span></div>
        </nav>
        <div className="sidebar-bottom"><span className="status-dot" /> LOCAL-FIRST · NO CLOUD AI</div>
      </aside>
      <main className="main">
        <div className="eyebrow">MISSION CONTROL / DISCOVERY</div>
        <div className="heading-row">
          <div>
            <h1>Opportunity Radar</h1>
            <p className="subtitle">A verifiable inventory of discovered openings. No AI guesses.</p>
          </div>
          <button className="refresh" onClick={() => void reload()} disabled={loading}>
            {loading ? 'Loading…' : '↻ Refresh'}
          </button>
        </div>
        {error && <div className="error" role="alert">{error}</div>}
        <div className="stats">
          <div className="metric"><span>KNOWN POSTINGS</span><strong>{jobs.length}</strong><small>Up to 500 shown</small></div>
          <div className="metric"><span>DISCOVERY SOURCES</span><strong>{briefing?.sources.filter(s => s.enabled).length ?? '—'}</strong><small>Market feeds + optional employers</small></div>
          <div className="metric"><span>LATEST SCAN</span><strong className="status-text">{lastScan?.status ?? 'Not run'}</strong><small>{lastScan ? fmt(lastScan.startedAtUtc) : 'Run the worker first'}</small></div>
          <div className="metric"><span>FIRST OBSERVED (7D)</span><strong>{briefing?.recentCount ?? '—'}</strong><small>{briefing ? briefing.candidateCount + ' candidates · ' + briefing.reviewCount + ' review' : 'Waiting for report'}</small></div>
        </div>
        <div className="evidence-notice" role="note">
          <b>{profileConfigured ? 'Private profile configured' : 'Private profile not configured'}</b>
          {' · '}Title-fit reviews consider metadata only. Requirement reviews quote actual employer descriptions,
          but listed skills are self-reported rather than verified experience.
          {profileConfigured ? ' Use the requirements CLI to add evidence reviews.' : ' Run profile init first.'}
        </div>
        <section className="panel">
          <div className="panel-header">
            <div><h2>Morning briefing</h2><p>First observed in the last 7 days — not necessarily newly posted or currently open.</p></div>
          </div>
          <div className="briefing-columns">
            <div>
              <h3>Discovery sources</h3>
              {!briefing || briefing.sources.length === 0 ?
                <p className="briefing-empty">No feeds available. Set up your local career profile to activate broad-market discovery.</p> :
                <div className="briefing-source-list">
                  {briefing.sources.map(source => <div className="briefing-source" key={source.provider + ':' + source.boardToken}>
                    <div><b>{source.displayName}</b><small>{source.provider === 'greenhouse' ? source.boardToken : 'Public cross-employer index'}{source.includeDescriptions ? ' · descriptions opted in' : ' · metadata only'}</small></div>
                    <div>
                      <span className={source.lastStatus === 'Succeeded' ? 'pill good' :
                        source.lastStatus === 'Failed' ? 'pill bad' : 'pill'}>{source.lastStatus}</span>
                      <small>{source.lastAttemptUtc ? fmt(source.lastAttemptUtc) : 'No scan yet'}</small>
                    </div>
                    {source.lastError && <p className="source-error">{source.lastError}</p>}
                  </div>)}
                </div>}
            </div>
            <div>
              <h3>Newly observed roles</h3>
              <p className="briefing-subtitle">Deterministic triage · {briefing?.candidateCount ?? 0} candidates, {briefing?.reviewCount ?? 0} for review</p>
              {!briefing || briefing.jobs.length === 0 ?
                <p className="briefing-empty">No recent qualifying observations. Create a profile and run daily to search across employers.</p> :
                <div className="briefing-job-list">
                  {briefing.jobs.slice(0, 12).map(job => <div className="briefing-job" key={job.id}>
                    <a href={job.jobUrl} target="_blank" rel="noopener noreferrer">{job.title} ↗</a>
                    <small>{job.company || 'Employer unspecified'} · {job.provider} · {job.location} · {job.triage} · first seen {fmt(job.firstSeenAtUtc)}</small>
                  </div>)}
                </div>}
              {briefing && briefing.jobs.length > 12 && <p className="briefing-subtitle">See more in the collected postings table below.</p>}
              {briefing && briefing.omittedCount > 0 && <p className="briefing-subtitle">{briefing.omittedCount} additional observations omitted from the briefing cap.</p>}
            </div>
          </div>
        </section>
        <section className="panel">
          <div className="panel-header">
            <div><h2>Collected postings</h2><p>Filtered locally; click a role to view the employer's original posting.</p></div>
            <input aria-label="Search discovered postings" value={query}
              placeholder="Filter by role, company, location…"
              onChange={e => setQuery(e.target.value)} />
          </div>
          <div className="table-scroller">
            <table><thead><tr><th>ROLE</th><th>COMPANY</th><th>SOURCE</th><th>LOCATION</th><th>LAST OBSERVED</th><th>AI TITLE FIT</th><th>REQUIREMENTS</th><th /></tr></thead>
              <tbody>{filtered.map(job => <tr key={job.id}>
                <td className="job-title">{job.title}</td><td>{job.company || '—'}</td>
                <td>{job.provider === 'greenhouse' ? job.boardToken : job.provider}</td>
                <td>{job.location}</td><td>{fmt(job.lastSeenAtUtc)}</td>
                <td>{evaluationByJob.has(job.id) ? (
                  <details className="fit-details">
                    <summary className="fit-pill">{evaluationByJob.get(job.id)!.fit}</summary>
                    <p>{evaluationByJob.get(job.id)!.rationale}</p>
                    <p className="uncertainty">{evaluationByJob.get(job.id)!.uncertainty}</p>
                    <small>{evaluationByJob.get(job.id)!.model} · metadata only</small>
                  </details>
                ) : <span className="pending-fit">Not evaluated</span>}</td>
                <td>{requirementsByJob.has(job.id) ? (
                  <details className="fit-details">
                    <summary className="fit-pill">
                      {requirementsByJob.get(job.id)!.listedSkillCount} listed-skill mentions
                    </summary>
                    {requirementsByJob.get(job.id)!.claims.map((claim, index) => (
                      <p className="requirement-claim" key={index}>
                        <q>{claim.quote}</q>
                        <small>{claim.profileSkill ? 'Profile lists: ' + claim.profileSkill : 'Unverified — not a proven gap'}</small>
                      </p>
                    ))}
                    <small>{requirementsByJob.get(job.id)!.model} · exact quotes · source checked</small>
                  </details>
                ) : <span className="pending-fit">{job.hasDescription ? 'Description collected' : 'No description'}</span>}</td>
                <td><a href={job.jobUrl} target="_blank" rel="noopener noreferrer">View ↗</a></td>
              </tr>)}</tbody></table>
            {!loading && filtered.length === 0 && <div className="empty">No postings in this view. Run a scan or change the filter.</div>}
          </div>
        </section>
        <section className="panel secondary-panel">
          <div className="panel-header"><div><h2>Collector activity</h2><p>Provider results and failures are recorded, never silently hidden.</p></div></div>
          {scans.length === 0 ? <div className="empty">No scans have been recorded.</div> :
            <div className="scan-list">{scans.slice(0, 8).map(scan => <div className="scan-row" key={scan.id}>
              <span className={scan.status === 'Succeeded' ? 'pill good' : 'pill bad'}>{scan.status}</span>
              <b>{scan.boardToken}</b><span>Found {scan.found} · Added {scan.added} · Updated {scan.updated} · Skipped {scan.skipped}</span>
              <time>{fmt(scan.startedAtUtc)}</time>
              {scan.error && <div className="scan-error">{scan.error}</div>}
            </div>)}</div>}
        </section>
        <footer>CareerForge v0.5 · Broad-index search using profile keywords · Indexes do not cover every company · Always verify the source listing.</footer>
      </main>
    </div>
  )
}
