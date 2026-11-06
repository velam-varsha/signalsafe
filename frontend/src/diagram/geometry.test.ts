import { describe, expect, it } from 'vitest'

import type { CheckResult } from '../api/types'
import westbrook from '../test/fixtures/westbrook-check.json'
import { nodePositions, pathOf, pointsLabel, signalGlyph, viewBoxOf } from './geometry'

const layout = (westbrook as unknown as CheckResult).layout
const signal = (id: string) => layout.signals.find((s) => s.id === id)!

describe('geometry', () => {
  it('turns a section line into SVG path data', () => {
    const t5 = layout.sections.find((s) => s.id === 'T5')!
    expect(pathOf(t5)).toBe('M290 110 L350 50 L560 50')
  })

  it('finds node positions from section ends', () => {
    const nodes = nodePositions(layout)
    expect(nodes.get('P1')).toEqual({ x: 290, y: 110 })
    expect(nodes.get('J8')).toEqual({ x: 330, y: 260 })
  })

  it('puts an eastbound signal above the track, pointing east', () => {
    const g = signalGlyph(signal('S1'), layout)!
    expect(g.dir).toEqual({ x: 1, y: 0 })
    expect(g.head.y).toBeLessThan(110)
    expect(g.head.x).toBeGreaterThan(170)
  })

  it('puts a westbound signal below the track, pointing west', () => {
    const g = signalGlyph(signal('S4'), layout)!
    expect(g.dir).toEqual({ x: -1, y: 0 })
    expect(g.head.y).toBeGreaterThan(200)
    expect(g.head.x).toBeLessThan(440)
  })

  it('reads a signal at the far end of its section in the right direction', () => {
    // S7 stands at J8, which is the end (endB) of T20; it reads back towards P4 (west).
    const g = signalGlyph(signal('S7'), layout)!
    expect(g.dir.x).toBeLessThan(0)
    expect(g.shunt).toBe(true)
  })

  it('puts a points label on the side away from the diverging leg', () => {
    const nodes = nodePositions(layout)
    expect(pointsLabel('P1', layout, nodes)!.y).toBeGreaterThan(110) // reverse leg goes up
    expect(pointsLabel('P4', layout, nodes)!.y).toBeLessThan(200) // reverse leg goes down
  })

  it('fits everything in the view box', () => {
    const box = viewBoxOf(layout)
    expect(box.x).toBeLessThan(20)
    expect(box.y).toBeLessThan(50)
    expect(box.x + box.width).toBeGreaterThan(740)
    expect(box.y + box.height).toBeGreaterThan(260)
  })
})
