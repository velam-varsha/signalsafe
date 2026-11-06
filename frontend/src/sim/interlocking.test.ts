import { describe, expect, it } from 'vitest'

import type { CheckResult } from '../api/types'
import kestrelJson from '../test/fixtures/kestrel-check.json'
import westbrookJson from '../test/fixtures/westbrook-check.json'
import {
  cancelRoute,
  completeRoute,
  initialState,
  lockedBy,
  requestRoute,
  signalAspect,
  type SimState,
  toggleOccupied,
  whyNot,
} from './interlocking'

const westbrook = westbrookJson as unknown as CheckResult
const kestrel = kestrelJson as unknown as CheckResult

function set(state: SimState, result: CheckResult, routeId: string): SimState {
  return completeRoute(requestRoute(state, result, routeId), result, routeId)
}

describe('interlocking simulator', () => {
  it('starts with every points Normal and every signal at danger', () => {
    const s = initialState(westbrook)
    expect(s.points).toEqual({ P1: 'Normal', P4: 'Normal' })
    expect(signalAspect(s, westbrook, 'S1')).toBe('danger')
  })

  it('moves and locks points, then clears the signal', () => {
    let s = requestRoute(initialState(westbrook), westbrook, 'S1(M)-S5')
    expect(s.points.P1).toBe('Reverse')
    expect(s.routes['S1(M)-S5']).toBe('setting')
    expect(signalAspect(s, westbrook, 'S1')).toBe('danger') // not cleared until the route is set

    s = completeRoute(s, westbrook, 'S1(M)-S5')
    expect(signalAspect(s, westbrook, 'S1')).toBe('clear')
    expect(lockedBy(s, westbrook, 'P1')?.id).toBe('S1(M)-S5')
    expect(s.log[0].message).toContain('S1 cleared')
  })

  it('refuses a conflicting route and says why', () => {
    const s = set(initialState(westbrook), westbrook, 'S1(M)-S3')

    expect(whyNot(s, westbrook, 'S1(M)-S5')).toContain('conflicts with S1(M)-S3')
    const after = requestRoute(s, westbrook, 'S1(M)-S5')
    expect(after.routes['S1(M)-S5']).toBeUndefined()
    expect(after.points.P1).toBe('Normal')
    expect(after.log[0].tone).toBe('bad')
  })

  it('allows a route that follows on from another', () => {
    const s = set(initialState(westbrook), westbrook, 'S2(M)-S4')

    expect(whyNot(s, westbrook, 'S4(M)-S6')).toBeNull()
  })

  it('puts the signal back to danger when its route or overlap is occupied', () => {
    let s = set(initialState(westbrook), westbrook, 'S2(M)-S4')
    expect(signalAspect(s, westbrook, 'S2')).toBe('clear')

    s = toggleOccupied(s, westbrook, 'T12') // the overlap of S2(M)-S4
    expect(signalAspect(s, westbrook, 'S2')).toBe('danger')
    expect(s.log[0].message).toContain('S2 returned to danger')

    s = toggleOccupied(s, westbrook, 'T12')
    expect(signalAspect(s, westbrook, 'S2')).toBe('clear')
  })

  it('refuses a route over an occupied section', () => {
    const s = toggleOccupied(initialState(westbrook), westbrook, 'T2')

    expect(whyNot(s, westbrook, 'S1(M)-S3')).toBe('T2 is occupied.')
  })

  it('releases the points when a route is cancelled', () => {
    let s = set(initialState(westbrook), westbrook, 'S1(M)-S3')
    s = cancelRoute(s, westbrook, 'S1(M)-S3')

    expect(lockedBy(s, westbrook, 'P1')).toBeUndefined()
    expect(signalAspect(s, westbrook, 'S1')).toBe('danger')
    expect(whyNot(s, westbrook, 'S1(M)-S5')).toBeNull()
  })

  it('moves both ends of a crossover together', () => {
    const s = requestRoute(initialState(kestrel), kestrel, 'S31(M)-S43')

    expect(s.points).toEqual({ P11: 'Reverse', P12: 'Reverse' })
  })

  it('explains a head-on conflict', () => {
    const s = set(initialState(kestrel), kestrel, 'S31(M)-S43')

    expect(whyNot(s, kestrel, 'S42(M)-S30')).toContain('head-on')
  })
})
