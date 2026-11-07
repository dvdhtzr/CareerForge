import { useEffect, useMemo, useState } from 'react'

type Job = {
  id: string
  boardToken: string
  title: string
  location: string
  jobUrl: string
  firstSeenAtUtc: string
  lastSeenAtUtc: string
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
  const [profileConfigured, setProfileConfigured] = useState(false)
  const [scans, setScans] = useState<Scan[]>([])
  const [query, setQuery] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  async function reload() {
    setLoading(true)
    try {
      const [jobsResponse, scansResponse, evaluationsResponse, profileResponse] = await Promise.all([
        fetch('/api/jobs?limit=500'), fetch('/api/scans'), fetch('/api/evaluations'),
        fetch('/api/profile/status'),
      ])
      if (!jobsResponse.ok || !scansResponse.ok || !evaluationsResponse.ok || !profileResponse.ok)
        throw new Error('API request failed')
      const [jobData, scanData, evaluationData, profileData]: [Job[], Scan[], Evaluation[], ProfileStatus] =
        await Promise.all([jobsResponse.json(), scansResponse.json(), evaluationsResponse.json(), profileResponse.json()])
      setJobs(jobData)
      setScans(scanData)
      setEvaluations(evaluationData)
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
    (j.title + ' ' + j.location + ' ' + j.boardToken).toLowerCase().includes(query.toLowerCase()),
  ), [jobs, query])
  const lastScan = scans[0]
  const evaluationByJob = useMemo(() => new Map(evaluations.map(e => [e.jobId, e])), [evaluations])

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
          <div className="metric"><span>SCAN RUNS</span><strong>{scans.length}</strong><small>Last 30 recorded</small></div>
          <div className="metric"><span>LATEST SCAN</span><strong className="status-text">{lastScan?.status ?? 'Not run'}</strong><small>{lastScan ? fmt(lastScan.startedAtUtc) : 'Run the worker first'}</small></div>
          <div className="metric"><span>LOCAL AI REVIEWS</span><strong>{evaluations.length}</strong><small>Preliminary metadata-only matches</small></div>
        </div>
        <div className="evidence-notice" role="note">
          <b>{profileConfigured ? 'Private profile configured' : 'Private profile not configured'}</b>
          {' · '}AI assessments consider titles and locations only, not qualifications, salary, or job descriptions.
          {profileConfigured ? ' Run the evaluate CLI to generate assessments.' : ' Run the profile init CLI first.'}
        </div>
        <section className="panel">
          <div className="panel-header">
            <div><h2>Collected postings</h2><p>Filtered locally; click a role to view the employer's original posting.</p></div>
            <input aria-label="Search discovered postings" value={query}
              placeholder="Filter by role, company, location…"
              onChange={e => setQuery(e.target.value)} />
          </div>
          <div className="table-scroller">
            <table><thead><tr><th>ROLE</th><th>BOARD</th><th>LOCATION</th><th>LAST OBSERVED</th><th>AI TITLE FIT</th><th /></tr></thead>
              <tbody>{filtered.map(job => <tr key={job.id}>
                <td className="job-title">{job.title}</td><td>{job.boardToken}</td>
                <td>{job.location}</td><td>{fmt(job.lastSeenAtUtc)}</td>
                <td>{evaluationByJob.has(job.id) ? (
                  <details className="fit-details">
                    <summary className="fit-pill">{evaluationByJob.get(job.id)!.fit}</summary>
                    <p>{evaluationByJob.get(job.id)!.rationale}</p>
                    <p className="uncertainty">{evaluationByJob.get(job.id)!.uncertainty}</p>
                    <small>{evaluationByJob.get(job.id)!.model} · metadata only</small>
                  </details>
                ) : <span className="pending-fit">Not evaluated</span>}</td>
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
        <footer>CareerForge v0.2 · Profile and AI results stay local · Preliminary AI judgments are not hiring requirements · Missing from a scan does not prove closure.</footer>
      </main>
    </div>
  )
}
