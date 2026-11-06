import type { CheckResult } from '../api/types'
import { lockedBy, type SimState, whyNot } from '../sim/interlocking'

interface Props {
  result: CheckResult
  state: SimState
  onSet: (routeId: string) => void
  onCancel: (routeId: string) => void
  onReset: () => void
}

export function SimulatorPanel({ result, state, onSet, onCancel, onReset }: Props) {
  return (
    <div className="pane">
      <p className="hint">
        Set a route to move and lock its points and clear its signal. Click a track section on the diagram to occupy or
        clear it, as if a train were there.
      </p>

      <ul className="list" aria-label="Routes">
        {result.routes.map((r) => {
          const status = state.routes[r.id]
          const blocked = status ? null : whyNot(state, result, r.id)
          return (
            <li key={r.id} className="sim-route">
              <div className="sim-row">
                <span className="r-id">{r.id}</span>
                <span className="sim-ctrl">
                  {status === 'set' && <span className="pill">Set</span>}
                  {status === 'setting' && <span className="pill setting">Setting…</span>}
                  {!status && <span className="pill idle">Not set</span>}
                  {status ? (
                    <button type="button" className="btn small cancel-btn" onClick={() => onCancel(r.id)} aria-label={`Cancel ${r.id}`}>
                      Cancel
                    </button>
                  ) : (
                    <button type="button" className="btn small set-btn" onClick={() => onSet(r.id)} aria-label={`Set ${r.id}`}>
                      Set
                    </button>
                  )}
                </span>
              </div>
              {blocked && <span className="blocked">Not available: {blocked}</span>}
            </li>
          )
        })}
      </ul>

      <h3>Points</h3>
      <div className="points-state">
        {result.layout.points.map((p) => {
          const holder = lockedBy(state, result, p.id)
          return (
            <div key={p.id} className="pt" data-testid={`points-state-${p.id}`}>
              <b>{p.id}</b> {state.points[p.id]}
              {holder ? <> · <span className="lk">locked by {holder.id}</span></> : ' · free'}
            </div>
          )
        })}
      </div>

      <div className="sim-row">
        <h3>Event log</h3>
        <button type="button" className="btn small" onClick={onReset}>Reset</button>
      </div>
      <ul className="log" aria-label="Event log">
        {state.log.length === 0 && (
          <li><span className="f-src">No events yet. Try setting {result.routes[0]?.id ?? 'a route'}, then a route that conflicts with it.</span></li>
        )}
        {state.log.map((l, i) => (
          <li key={`${l.time}-${i}`}>
            <time>{l.time}</time>
            <span className={l.tone === 'info' ? '' : l.tone}>{l.message}</span>
          </li>
        ))}
      </ul>
      <p className="f-src">
        Simplified: no approach locking, timers or train movement. For learning and checking, not for operational use.
      </p>
    </div>
  )
}
