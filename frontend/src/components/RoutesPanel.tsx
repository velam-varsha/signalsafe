import type { CheckResult, Route } from '../api/types'

interface Props {
  result: CheckResult
  selected: string | null
  onSelect: (id: string | null) => void
}

export function RoutesPanel({ result, selected, onSelect }: Props) {
  const missing = result.routes.filter((r) => !r.inTables).length
  const conflictsOf = (id: string) =>
    result.conflicts
      .filter((c) => c.routeA === id || c.routeB === id)
      .map((c) => ({ other: c.routeA === id ? c.routeB : c.routeA, opposing: c.opposing }))

  return (
    <div className="pane">
      <p className="hint">
        {result.routes.length} routes found by searching the track layout.
        {result.tables && ` ${missing === 0 ? 'All are' : `${missing} ${missing === 1 ? 'is' : 'are'} not`} in the control tables.`}{' '}
        Select a route to preview it.
      </p>
      <ul className="list">
        {result.routes.map((r) => (
          <li key={r.id}>
            <RouteCard
              route={r}
              conflicts={conflictsOf(r.id)}
              showTableStatus={result.tables !== null}
              active={selected === r.id}
              onClick={() => onSelect(selected === r.id ? null : r.id)}
            />
          </li>
        ))}
      </ul>
    </div>
  )
}

function RouteCard({ route: r, conflicts, showTableStatus, active, onClick }: {
  route: Route
  conflicts: { other: string; opposing: boolean }[]
  showTableStatus: boolean
  active: boolean
  onClick: () => void
}) {
  const points = Object.entries(r.points)
  return (
    <button type="button" className="route" aria-pressed={active} onClick={onClick}>
      <span className="r-top">
        <span className="r-id">{r.id}</span>
        {showTableStatus && (r.inTables ? <span className="pill">In tables</span> : <span className="pill miss">Missing</span>)}
      </span>
      <dl className="kv">
        <dt>Type</dt>
        <dd>{r.isShunt ? 'Shunt' : 'Main'}</dd>
        <dt>Points</dt>
        <dd>
          {points.length === 0
            ? 'None'
            : points.map(([id, pos]) => `${id} ${pos}${r.linkedPoints.includes(id) ? ' (crossover end)' : ''}`).join(', ')}
        </dd>
        <dt>Sections</dt>
        <dd>{r.sections.join(', ')}</dd>
        <dt>Overlap</dt>
        <dd>{r.overlap.join(', ') || 'None'}</dd>
        <dt>Conflicts</dt>
        <dd>
          {conflicts.length === 0
            ? 'None'
            : conflicts.map((c) => `${c.other}${c.opposing ? ' (head-on)' : ''}`).join(', ')}
        </dd>
      </dl>
    </button>
  )
}
