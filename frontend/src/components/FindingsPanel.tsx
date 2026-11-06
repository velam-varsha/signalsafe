import { useState } from 'react'

import type { CheckResult, Finding } from '../api/types'

interface Props {
  result: CheckResult
  selected: number | null
  elementFilter: string | null
  onSelect: (index: number | null) => void
  onClearElement: () => void
}

type Filter = 'all' | 'Error' | 'Warning'

export function FindingsPanel({ result, selected, elementFilter, onSelect, onClearElement }: Props) {
  const [filter, setFilter] = useState<Filter>('all')
  const findings = result.findings
  const errors = findings.filter((f) => f.severity === 'Error').length
  const warnings = findings.length - errors
  const passed = result.rules.filter((r) => r.findingCount === 0)

  if (!result.tables) {
    return (
      <div className="pane">
        <p className="hint">
          Only a layout was checked. Add control tables (Excel) to compare them with the {result.routes.length} routes the
          layout allows.
        </p>
      </div>
    )
  }

  const visible = findings
    .map((f, i) => ({ f, i }))
    .filter(({ f }) => filter === 'all' || f.severity === filter)
    .filter(({ f }) => !elementFilter || f.elements.includes(elementFilter) || f.routeId === elementFilter)

  return (
    <div className="pane">
      <div className="stats">
        <div className="stat e"><div className="n">{errors}</div><div className="k">Errors</div></div>
        <div className="stat w"><div className="n">{warnings}</div><div className="k">Warnings</div></div>
        <div className="stat o"><div className="n">{passed.length}</div><div className="k">Rules passed</div></div>
      </div>

      <div className="filters" role="group" aria-label="Filter findings">
        {(['all', 'Error', 'Warning'] as const).map((k) => (
          <button key={k} type="button" className="chipbtn" aria-pressed={filter === k} onClick={() => setFilter(k)}>
            {k === 'all' ? `All ${findings.length}` : k === 'Error' ? `Errors ${errors}` : `Warnings ${warnings}`}
          </button>
        ))}
      </div>

      {elementFilter && (
        <p className="element-filter">
          Showing findings for <b className="mono">{elementFilter}</b>
          <button type="button" className="linkbtn" onClick={onClearElement}>Show all</button>
        </p>
      )}

      {findings.length === 0 ? (
        <p className="all-clear">No findings. The control tables agree with the layout for all {result.rules.length} rules.</p>
      ) : visible.length === 0 ? (
        <p className="hint">No findings match this filter.</p>
      ) : (
        <>
          <p className="hint">Select a finding to see it on the diagram.</p>
          <ul className="list">
            {visible.map(({ f, i }) => (
              <li key={i}>
                <FindingCard finding={f} active={selected === i} onClick={() => onSelect(selected === i ? null : i)} />
              </li>
            ))}
          </ul>
        </>
      )}

      {passed.length > 0 && (
        <details>
          <summary>{passed.length} rules passed</summary>
          <ul className="passed">
            {passed.map((r) => (
              <li key={r.id}><span className="tick">✓</span><span><b>{r.id}</b> {r.name}</span></li>
            ))}
          </ul>
        </details>
      )}
    </div>
  )
}

function FindingCard({ finding: f, active, onClick }: { finding: Finding; active: boolean; onClick: () => void }) {
  const sev = f.severity === 'Error' ? 'error' : 'warning'
  const where = f.source
    ? `${f.source.sheet} sheet · row ${f.source.row}${f.source.column ? ` · ${f.source.column}` : ''}`
    : 'Derived from layout'
  return (
    <button type="button" className={`finding ${sev}`} aria-pressed={active} onClick={onClick}>
      <span className="f-top">
        <span className="f-rule">{f.ruleId} · {f.ruleName}</span>
        <span className={`sev ${sev}`}>{f.severity}</span>
      </span>
      {f.routeId && <span className="f-route mono">{f.routeId}</span>}
      <span className="f-msg">{f.message}</span>
      <span className="f-src">{where}</span>
    </button>
  )
}
