import { useMemo } from 'react'

import type { LayoutView, Route, Severity } from '../api/types'
import { nodePositions, pathOf, pointsLabel, sectionLabel, signalGlyph, viewBoxOf } from '../diagram/geometry'

/** What the simulator shows on the diagram. */
export interface SimView {
  kind: 'sim'
  setSections: Set<string>
  overlapSections: Set<string>
  occupied: Set<string>
  points: Record<string, 'Normal' | 'Reverse'>
  locked: Set<string>
  clearSignals: Set<string>
}

export type Highlight =
  | { kind: 'finding'; severity: Severity; elements: string[] }
  | { kind: 'route'; route: Route }
  | SimView
  | null

interface Props {
  layout: LayoutView
  highlight: Highlight
  selectedElement: string | null
  onElementClick: (id: string) => void
}

type TrackState = '' | 'err' | 'warn' | 'prev' | 'ovl' | 'faded' | 'set' | 'ovlset' | 'occ'

export function Schematic({ layout, highlight, selectedElement, onElementClick }: Props) {
  const nodes = useMemo(() => nodePositions(layout), [layout])
  const box = useMemo(() => viewBoxOf(layout), [layout])
  const glyphs = useMemo(
    () => layout.signals.map((s) => signalGlyph(s, layout)).filter((g) => g !== null),
    [layout],
  )

  if (!layout.hasDiagram) {
    return (
      <div className="no-diagram">
        <p>This layout has no drawing coordinates, so it cannot be drawn.</p>
        <p>Add a <code>diagram</code> block to the layout file to see it here. The checks still run.</p>
      </div>
    )
  }

  const stateOf = (id: string): TrackState => {
    if (!highlight) return ''
    if (highlight.kind === 'sim') {
      if (highlight.occupied.has(id)) return 'occ'
      if (highlight.setSections.has(id)) return 'set'
      if (highlight.overlapSections.has(id)) return 'ovlset'
      return ''
    }
    if (highlight.kind === 'finding') {
      if (!highlight.elements.includes(id)) return 'faded'
      return highlight.severity === 'Error' ? 'err' : 'warn'
    }
    if (highlight.route.sections.includes(id)) return 'prev'
    if (highlight.route.overlap.includes(id)) return 'ovl'
    return 'faded'
  }

  const isMarked = (id: string): boolean => {
    if (selectedElement === id) return true
    if (highlight?.kind === 'finding') return highlight.elements.includes(id)
    if (highlight?.kind === 'sim') return false
    if (highlight?.kind === 'route') {
      const r = highlight.route
      return r.entrance === id || r.exit === id || id in r.points
    }
    return false
  }

  const markColour = highlight?.kind === 'finding'
    ? highlight.severity === 'Error' ? 'var(--vdu-err)' : 'var(--vdu-warn)'
    : 'var(--vdu-prev)'

  const routePosition = (pointsId: string) =>
    highlight?.kind === 'route' ? highlight.route.points[pointsId]
      : highlight?.kind === 'sim' ? highlight.points[pointsId]
        : undefined

  return (
    <svg
      className="schematic"
      viewBox={`${box.x} ${box.y} ${box.width} ${box.height}`}
      role="img"
      aria-label={`Track schematic of ${layout.name}`}
    >
      <rect x={box.x} y={box.y} width={box.width} height={box.height} fill="var(--vdu-bg)" />

      {layout.sections.map((s) => {
        const state = stateOf(s.id)
        const d = pathOf(s)
        const label = sectionLabel(s)
        return (
          <g key={s.id} data-testid={`section-${s.id}`} data-state={state || 'idle'}>
            {(state === 'err' || state === 'warn' || state === 'prev' || state === 'occ' || state === 'set') && (
              <path className={`halo ${state}`} d={d} />
            )}
            <path className={`trk ${state}`} d={d} />
            <path
              className="hit"
              d={d}
              onClick={() => onElementClick(s.id)}
              role="button"
              aria-label={`Track section ${s.id}`}
            >
              <title>{highlight?.kind === 'sim' ? `${s.id}: click to occupy or clear` : `${s.id} · ${s.lengthM} m`}</title>
            </path>
            {label && (
              <text className={`lbl${state && state !== 'faded' ? ' strong' : ''}`} x={label.x} y={label.y} textAnchor="middle">
                {s.id}
              </text>
            )}
          </g>
        )
      })}

      {layout.points.map((p) => {
        const at = nodes.get(p.id)
        const label = pointsLabel(p.id, layout, nodes)
        if (!at || !label) return null
        const position = routePosition(p.id)
        return (
          <g
            key={p.id}
            data-testid={`points-${p.id}`}
            className="clickable"
            onClick={() => onElementClick(p.id)}
            role="button"
            aria-label={`Points ${p.id}`}
          >
            {isMarked(p.id) && <circle cx={at.x} cy={at.y} r={11} fill="none" stroke={markColour} strokeWidth={2} />}
            <circle cx={at.x} cy={at.y} r={3.5} fill="var(--vdu-label)" />
            <text className="lbl strong" x={label.x} y={label.y} textAnchor="middle">
              {p.id}
              {position ? ` ${position === 'Normal' ? 'N' : 'R'}` : ''}
              {highlight?.kind === 'sim' && highlight.locked.has(p.id) ? ' · L' : ''}
            </text>
          </g>
        )
      })}

      {glyphs.map((g) => {
        const proceed = highlight?.kind === 'route'
          ? highlight.route.entrance === g.id
          : highlight?.kind === 'sim' && highlight.clearSignals.has(g.id)
        const colour = proceed ? (g.shunt ? 'var(--vdu-set)' : 'var(--vdu-green)') : 'var(--vdu-red)'
        return (
          <g
            key={g.id}
            data-testid={`signal-${g.id}`}
            className="clickable"
            onClick={() => onElementClick(g.id)}
            role="button"
            aria-label={`Signal ${g.id}`}
          >
            <line className="post" x1={g.base.x} y1={g.base.y} x2={g.postTop.x} y2={g.postTop.y} />
            <line className="post" x1={g.postTop.x} y1={g.postTop.y} x2={g.head.x - g.dir.x * 5} y2={g.head.y - g.dir.y * 5} />
            {isMarked(g.id) && <circle cx={g.head.x} cy={g.head.y} r={11} fill="none" stroke={markColour} strokeWidth={2} />}
            {g.shunt ? (
              <>
                <rect x={g.head.x - 5} y={g.head.y - 5} width={10} height={10} rx={2} fill="#1b2635" stroke="var(--vdu-label)" />
                <circle cx={g.head.x} cy={g.head.y} r={2.6} fill={colour} />
              </>
            ) : (
              <>
                <circle cx={g.head.x} cy={g.head.y} r={6} fill="#1b2635" stroke="var(--vdu-label)" />
                <circle cx={g.head.x} cy={g.head.y} r={4} fill={colour} />
              </>
            )}
            <text className="lbl strong" x={g.label.x} y={g.label.y + 4} textAnchor="middle">
              {g.id}
            </text>
          </g>
        )
      })}
    </svg>
  )
}
