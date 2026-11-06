import type { LayoutView, Section, Signal } from '../api/types'

export type Vec = { x: number; y: number }

const add = (a: Vec, b: Vec): Vec => ({ x: a.x + b.x, y: a.y + b.y })
const scale = (a: Vec, k: number): Vec => ({ x: a.x * k, y: a.y * k })
const unit = (a: Vec): Vec => {
  const len = Math.hypot(a.x, a.y) || 1
  return { x: a.x / len, y: a.y / len }
}
const toVec = ([x, y]: [number, number]): Vec => ({ x, y })

/** SVG path data for a section's line. */
export function pathOf(section: Section): string {
  return (section.line ?? []).map(([x, y], i) => `${i === 0 ? 'M' : 'L'}${x} ${y}`).join(' ')
}

/** Where to put a section's label: just above the middle of its longest straight piece. */
export function sectionLabel(section: Section): Vec | null {
  const line = section.line
  if (!line || line.length < 2) return null
  let best = 0
  let bestLen = -1
  for (let i = 0; i < line.length - 1; i++) {
    const len = Math.hypot(line[i + 1][0] - line[i][0], line[i + 1][1] - line[i][1])
    if (len > bestLen) {
      bestLen = len
      best = i
    }
  }
  const a = toVec(line[best])
  const b = toVec(line[best + 1])
  return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 - 9 }
}

/** The drawing position of every node (joint, line end or points), taken from the section ends. */
export function nodePositions(layout: LayoutView): Map<string, Vec> {
  const nodes = new Map<string, Vec>()
  for (const s of layout.sections) {
    if (!s.line || s.line.length < 2) continue
    if (!nodes.has(s.endA)) nodes.set(s.endA, toVec(s.line[0]))
    if (!nodes.has(s.endB)) nodes.set(s.endB, toVec(s.line[s.line.length - 1]))
  }
  return nodes
}

export interface SignalGlyph {
  id: string
  shunt: boolean
  /** Where the post meets the track. */
  base: Vec
  /** Top of the post. */
  postTop: Vec
  /** Centre of the signal head. */
  head: Vec
  label: Vec
  /** Unit vector in the direction the signal reads. */
  dir: Vec
}

/**
 * Places a signal beside the track at its node, on the left-hand side in its direction of travel
 * (as in GB practice), with its head pointing the way it reads.
 */
export function signalGlyph(signal: Signal, layout: LayoutView): SignalGlyph | null {
  const section = layout.sections.find((s) => s.id === signal.protects)
  if (!section?.line || section.line.length < 2) return null
  const fromA = section.endA === signal.atNode
  const line = fromA ? section.line : [...section.line].reverse()
  const base = toVec(line[0])
  const dir = unit({ x: line[1][0] - base.x, y: line[1][1] - base.y })
  // Left of the direction of travel, in screen coordinates (y points down).
  const left: Vec = { x: dir.y, y: -dir.x }
  const postTop = add(base, scale(left, 16))
  return {
    id: signal.id,
    shunt: signal.type === 'Shunt',
    base: add(base, scale(left, 5)),
    postTop,
    head: add(postTop, scale(dir, 8)),
    label: add(add(base, scale(left, 30)), scale(dir, 8)),
    dir,
  }
}

/** Where to put a points label: on the opposite side from the diverging (reverse) leg. */
export function pointsLabel(pointsId: string, layout: LayoutView, nodes: Map<string, Vec>): Vec | null {
  const at = nodes.get(pointsId)
  const points = layout.points.find((p) => p.id === pointsId)
  const reverse = layout.sections.find((s) => s.id === points?.reverse)
  if (!at || !reverse?.line || reverse.line.length < 2) return at ?? null
  const line = reverse.endA === pointsId ? reverse.line : [...reverse.line].reverse()
  const goesUp = line[1][1] < at.y
  return { x: at.x, y: goesUp ? at.y + 22 : at.y - 12 }
}

/** A viewBox that fits everything drawn, with room for labels. */
export function viewBoxOf(layout: LayoutView, pad = 44): { x: number; y: number; width: number; height: number } {
  const pts = layout.sections.flatMap((s) => s.line ?? [])
  if (pts.length === 0) return { x: 0, y: 0, width: 760, height: 300 }
  const xs = pts.map((p) => p[0])
  const ys = pts.map((p) => p[1])
  const minX = Math.min(...xs) - pad
  const minY = Math.min(...ys) - pad
  return { x: minX, y: minY, width: Math.max(...xs) + pad - minX, height: Math.max(...ys) + pad - minY }
}
