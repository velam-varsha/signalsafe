import { useCallback, useEffect, useMemo, useState } from 'react'

import { ApiError, checkFiles, checkSample, downloadReport, listSamples, sampleFile, saveBlob, templateBlob } from './api/client'
import type { CheckResult, SampleInfo } from './api/types'
import { FindingsPanel } from './components/FindingsPanel'
import { RoutesPanel } from './components/RoutesPanel'
import { type Highlight, Schematic } from './components/Schematic'
import { SimulatorPanel } from './components/SimulatorPanel'
import { cancelRoute, completeRoute, initialState, requestRoute, signalAspect, type SimState, toggleOccupied } from './sim/interlocking'

type Source =
  | { kind: 'sample'; id: string }
  | { kind: 'upload'; layout: File; tables: File | null }

type Tab = 'findings' | 'routes' | 'sim'

const SETTING_TIME_MS = 800

const SAMPLE_FILES: Record<string, { layout: string; tables: string }> = {
  westbrook: { layout: 'westbrook-junction.json', tables: 'westbrook-control-tables.xlsx' },
  kestrel: { layout: 'kestrel-lane-crossover.json', tables: 'kestrel-control-tables.xlsx' },
}

export default function App() {
  const [samples, setSamples] = useState<SampleInfo[]>([])
  const [source, setSource] = useState<Source>({ kind: 'sample', id: 'westbrook' })
  const [result, setResult] = useState<CheckResult | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState<string | null>(null)
  const [tab, setTab] = useState<Tab>('findings')
  const [selectedFinding, setSelectedFinding] = useState<number | null>(null)
  const [selectedRoute, setSelectedRoute] = useState<string | null>(null)
  const [elementFilter, setElementFilter] = useState<string | null>(null)
  const [uploadOpen, setUploadOpen] = useState(false)
  const [layoutFile, setLayoutFile] = useState<File | null>(null)
  const [tablesFile, setTablesFile] = useState<File | null>(null)
  const [sim, setSim] = useState<SimState | null>(null)

  const showToast = useCallback((message: string) => {
    setToast(message)
    window.setTimeout(() => setToast(null), 3200)
  }, [])

  // Fetches and shows a check. Only sets state after awaiting, so it is safe to call from an effect.
  const load = useCallback(async (next: Source) => {
    try {
      const r = next.kind === 'sample' ? await checkSample(next.id) : await checkFiles(next.layout, next.tables)
      setResult(r)
      setSim(initialState(r))
      setSource(next)
      setSelectedRoute(null)
      setElementFilter(null)
      setSelectedFinding(r.findings.length > 0 ? 0 : null)
      setTab(r.tables ? 'findings' : 'routes')
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong while checking.')
    } finally {
      setLoading(false)
    }
  }, [])

  const run = async (next: Source) => {
    setLoading(true)
    setError(null)
    await load(next)
  }

  useEffect(() => {
    listSamples().then(setSamples).catch(() => setSamples([]))
    // Loading data from the API on first render is what this effect is for; state is only set after the fetch.
    // oxlint-disable-next-line react/set-state-in-effect
    void load({ kind: 'sample', id: 'westbrook' })
  }, [load])

  const highlight: Highlight = useMemo(() => {
    if (!result) return null
    if (tab === 'findings' && selectedFinding !== null && result.findings[selectedFinding]) {
      const f = result.findings[selectedFinding]
      return { kind: 'finding', severity: f.severity, elements: f.elements }
    }
    if (tab === 'sim' && sim) {
      const set = result.routes.filter((r) => sim.routes[r.id])
      return {
        kind: 'sim',
        setSections: new Set(set.flatMap((r) => r.sections)),
        overlapSections: new Set(set.flatMap((r) => r.overlap)),
        occupied: new Set(sim.occupied),
        points: sim.points,
        locked: new Set(set.flatMap((r) => Object.keys(r.points))),
        clearSignals: new Set(result.layout.signals.map((g) => g.id).filter((id) => signalAspect(sim, result, id) === 'clear')),
      }
    }
    if (tab === 'routes' && selectedRoute) {
      const route = result.routes.find((r) => r.id === selectedRoute)
      return route ? { kind: 'route', route } : null
    }
    return null
  }, [result, tab, selectedFinding, selectedRoute, sim])

  const onSetRoute = (routeId: string) => {
    if (!result) return
    setSim((s) => (s ? requestRoute(s, result, routeId) : s))
    window.setTimeout(() => setSim((s) => (s ? completeRoute(s, result, routeId) : s)), SETTING_TIME_MS)
  }

  const onElementClick = (id: string) => {
    if (tab === 'sim') {
      if (result && sim && result.layout.sections.some((s) => s.id === id)) setSim(toggleOccupied(sim, result, id))
      return
    }
    if (!result?.tables) return
    setTab('findings')
    setSelectedFinding(null)
    setElementFilter((current) => (current === id ? null : id))
  }

  const onReport = async () => {
    try {
      let files: { layout: File; tables: File }
      if (source.kind === 'sample') {
        const names = SAMPLE_FILES[source.id]
        files = {
          layout: await sampleFile(source.id, 'layout', names.layout),
          tables: await sampleFile(source.id, 'tables', names.tables),
        }
      } else if (source.tables) {
        files = { layout: source.layout, tables: source.tables }
      } else {
        showToast('Add control tables to download a findings report.')
        return
      }
      saveBlob(await downloadReport(files.layout, files.tables), 'signalsafe-findings.xlsx')
      showToast('Findings report downloaded.')
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : 'The report could not be downloaded.')
    }
  }

  const onTemplate = async () => {
    try {
      saveBlob(await templateBlob(), 'control-table-template.xlsx')
      showToast('Blank control table template downloaded.')
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : 'The template could not be downloaded.')
    }
  }

  const onCheckUpload = async () => {
    if (!layoutFile) return
    setUploadOpen(false)
    await run({ kind: 'upload', layout: layoutFile, tables: tablesFile })
  }

  const fileNames =
    source.kind === 'sample'
      ? SAMPLE_FILES[source.id]
      : { layout: source.layout.name, tables: source.tables?.name ?? null }
  const errorCount = result?.findings.filter((f) => f.severity === 'Error').length ?? 0

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <div className="logo" aria-hidden="true">
            <svg width="22" height="22" viewBox="0 0 22 22"><rect x="9.5" y="9" width="3" height="12" rx="1" fill="#93a3b6" /><rect x="5" y="1" width="12" height="11" rx="5.5" fill="#1b2635" /><circle cx="11" cy="6.5" r="3.2" fill="#3ee07f" /></svg>
          </div>
          <div>
            <h1>SignalSafe</h1>
            <div className="sub">
              <span>{result?.layout.name ?? 'Loading…'}</span>
              {source.kind === 'sample' && <span className="tag">Example data</span>}
            </div>
          </div>
        </div>
        <div className="actions">
          <label className="select">
            <span className="visually-hidden">Sample layout</span>
            <select
              id="sample-select"
              value={source.kind === 'sample' ? source.id : ''}
              onChange={(e) => e.target.value && void run({ kind: 'sample', id: e.target.value })}
            >
              {source.kind === 'upload' && <option value="">Your files</option>}
              {(samples.length ? samples : [{ id: 'westbrook', name: 'Westbrook Junction', description: '' }]).map((s) => (
                <option key={s.id} value={s.id}>Sample: {s.name}</option>
              ))}
            </select>
          </label>
          <button type="button" className="btn" onClick={() => setUploadOpen((o) => !o)} aria-expanded={uploadOpen}>
            Upload files
          </button>
          <button type="button" className="btn" onClick={onTemplate}>Template</button>
          <button type="button" className="btn primary" onClick={onReport} disabled={!result?.tables}>
            Download report
          </button>
        </div>
      </header>

      {uploadOpen && (
        <section className="upload" aria-label="Upload files">
          <div className="field">
            <label htmlFor="layout-file">Layout (.json)</label>
            <input id="layout-file" type="file" accept=".json,application/json" onChange={(e) => setLayoutFile(e.target.files?.[0] ?? null)} />
          </div>
          <div className="field">
            <label htmlFor="tables-file">Control tables (.xlsx, optional)</label>
            <input id="tables-file" type="file" accept=".xlsx" onChange={(e) => setTablesFile(e.target.files?.[0] ?? null)} />
          </div>
          <button type="button" className="btn primary" disabled={!layoutFile} onClick={onCheckUpload}>Check files</button>
          <p className="hint">Files are checked in memory and not stored.</p>
        </section>
      )}

      <section className="strip" aria-label="Loaded files">
        <div className="files">
          <span className="file"><b>LAYOUT</b><span className="mono">{fileNames.layout}</span></span>
          <span className="file"><b>TABLES</b><span className="mono">{fileNames.tables ?? 'none'}</span></span>
        </div>
        <span className="spacer" />
        <span className="run-state">
          {loading
            ? 'Checking…'
            : result && (
              <>
                <strong>{result.routes.length} routes</strong> derived
                {result.tables && <> · <strong>{result.rules.length} rules</strong> run</>} · checked in {Math.max(1, Math.round(result.durationMs))} ms
              </>
            )}
        </span>
      </section>

      {error && <div className="banner error" role="alert">{error}</div>}

      {result && result.layoutIssues.length > 0 && (
        <div className="banner error" role="alert">
          <b>The layout has {result.layoutIssues.length} problem(s), so no routes were derived:</b>
          <ul>{result.layoutIssues.map((i) => <li key={`${i.code}-${i.elementId}-${i.message}`}>[{i.code}] {i.message}</li>)}</ul>
        </div>
      )}

      {result && (
        <div className="main">
          <section className="screen" aria-label="Track schematic">
            <div className="screen-head">
              <span className="loc">{result.layout.name}</span>
              <span className="mode">{tab === 'findings' ? 'FINDINGS' : tab === 'routes' ? 'ROUTES' : 'SIMULATOR'}</span>
            </div>
            <div className="svg-wrap">
              <Schematic
                layout={result.layout}
                highlight={highlight}
                selectedElement={elementFilter}
                onElementClick={onElementClick}
              />
            </div>
            {tab === 'sim' ? (
              <div className="legend">
                <span><i style={{ background: 'var(--vdu-track)' }} />Track</span>
                <span><i style={{ background: 'var(--vdu-set)' }} />Route set</span>
                <span><i className="dashed set" />Overlap</span>
                <span><i style={{ background: 'var(--vdu-occ)' }} />Occupied</span>
                <span className="legend-hint">Click a section to occupy or clear it.</span>
              </div>
            ) : (
              <div className="legend">
                <span><i style={{ background: 'var(--vdu-track)' }} />Track</span>
                <span><i style={{ background: 'var(--vdu-err)' }} />Error</span>
                <span><i style={{ background: 'var(--vdu-warn)' }} />Warning</span>
                <span><i style={{ background: 'var(--vdu-prev)' }} />Route preview</span>
                <span><i className="dashed" />Overlap</span>
                <span className="legend-hint">Click a section, signal or points to see its findings.</span>
              </div>
            )}
          </section>

          <aside className="panel">
            <div className="tabs" role="tablist">
              <button type="button" role="tab" className="tab" aria-selected={tab === 'findings'} onClick={() => setTab('findings')}>
                Findings {result.tables && <span className="count">{result.findings.length}</span>}
              </button>
              <button type="button" role="tab" className="tab" aria-selected={tab === 'routes'} onClick={() => setTab('routes')}>
                Routes <span className="count neutral">{result.routes.length}</span>
              </button>
              <button type="button" role="tab" className="tab" aria-selected={tab === 'sim'} onClick={() => setTab('sim')}>
                Simulator
              </button>
            </div>
            {tab === 'sim' && sim ? (
              <SimulatorPanel
                result={result}
                state={sim}
                onSet={onSetRoute}
                onCancel={(id) => setSim(cancelRoute(sim, result, id))}
                onReset={() => setSim(initialState(result))}
              />
            ) : tab === 'findings' ? (
              <FindingsPanel
                result={result}
                selected={selectedFinding}
                elementFilter={elementFilter}
                onSelect={setSelectedFinding}
                onClearElement={() => setElementFilter(null)}
              />
            ) : (
              <RoutesPanel result={result} selected={selectedRoute} onSelect={setSelectedRoute} />
            )}
          </aside>
        </div>
      )}

      <p className="foot">
        {source.kind === 'sample' && 'Invented example data. '}
        {result?.tables && `${errorCount} error(s) found. `}
        SignalSafe is an advisory design-checking aid, not a certified safety system.
      </p>

      <div className={`toast${toast ? ' show' : ''}`} role="status" aria-live="polite">{toast}</div>
    </div>
  )
}
