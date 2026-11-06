import type { CheckResult, PointPosition, Route } from '../api/types'

/**
 * A simple interlocking simulator, driven by the routes and conflicts the engine derived.
 *
 * It follows the basic order of a real interlocking: check the route is available, move and lock
 * its points, then clear the entrance signal. A signal shows danger again as soon as any section
 * on its route or overlap is occupied.
 *
 * Simplifications for this demo: no approach locking or timers, and no train movement.
 */

export type RouteStatus = 'setting' | 'set'

export interface LogEntry {
  time: string
  message: string
  tone: 'info' | 'good' | 'bad'
}

export interface SimState {
  routes: Record<string, RouteStatus>
  occupied: string[]
  points: Record<string, PointPosition>
  log: LogEntry[]
}

export type Aspect = 'clear' | 'danger'

const MAX_LOG = 40

export function initialState(result: CheckResult): SimState {
  return {
    routes: {},
    occupied: [],
    points: Object.fromEntries(result.layout.points.map((p) => [p.id, 'Normal' as PointPosition])),
    log: [],
  }
}

const routeById = (result: CheckResult, id: string): Route | undefined => result.routes.find((r) => r.id === id)

const setRoutes = (state: SimState, result: CheckResult): Route[] =>
  Object.keys(state.routes)
    .map((id) => routeById(result, id))
    .filter((r): r is Route => r !== undefined)

function addLog(state: SimState, message: string, tone: LogEntry['tone'], now: Date): SimState {
  const time = now.toTimeString().slice(0, 8)
  return { ...state, log: [{ time, message, tone }, ...state.log].slice(0, MAX_LOG) }
}

const word = (p: PointPosition) => (p === 'Normal' ? 'Normal' : 'Reverse')

/** The set route that holds these points locked, if any. */
export function lockedBy(state: SimState, result: CheckResult, pointsId: string): Route | undefined {
  return setRoutes(state, result).find((r) => pointsId in r.points)
}

/** Why a route cannot be set right now, or null if it can. */
export function whyNot(state: SimState, result: CheckResult, routeId: string): string | null {
  const route = routeById(result, routeId)
  if (!route) return `${routeId} is not a route in this layout.`
  if (state.routes[routeId]) return `${routeId} is already set.`

  for (const other of setRoutes(state, result)) {
    const conflict = result.conflicts.find(
      (c) => (c.routeA === routeId && c.routeB === other.id) || (c.routeB === routeId && c.routeA === other.id),
    )
    if (conflict) {
      const reason = conflict.opposing
        ? `they would meet head-on over ${conflict.sharedSections.join(', ')}`
        : conflict.sharedSections.length > 0
          ? `both use ${conflict.sharedSections.join(', ')}`
          : `they need ${conflict.opposingPoints.join(', ')} in opposite positions`
      return `${routeId} conflicts with ${other.id}, which is set: ${reason}.`
    }
  }

  for (const [pointsId, needed] of Object.entries(route.points)) {
    const holder = lockedBy(state, result, pointsId)
    if (holder && state.points[pointsId] !== needed) {
      return `${pointsId} is locked ${word(state.points[pointsId])} by ${holder.id}.`
    }
  }

  const occupied = route.sections.filter((s) => state.occupied.includes(s))
  if (occupied.length > 0) return `${occupied.join(', ')} ${occupied.length === 1 ? 'is' : 'are'} occupied.`

  return null
}

/** Asks for a route. If it is available, its points move and lock and it starts setting. */
export function requestRoute(state: SimState, result: CheckResult, routeId: string, now = new Date()): SimState {
  const reason = whyNot(state, result, routeId)
  if (reason) return addLog(state, `Refused ${routeId}: ${reason}`, 'bad', now)

  const route = routeById(result, routeId)!
  const moved = Object.entries(route.points)
    .filter(([id, pos]) => state.points[id] !== pos)
    .map(([id, pos]) => `${id} to ${word(pos)}`)

  const next: SimState = {
    ...state,
    points: { ...state.points, ...route.points },
    routes: { ...state.routes, [routeId]: 'setting' },
  }
  return addLog(next, `Setting ${routeId}${moved.length ? `: moving ${moved.join(', ')}` : ''}.`, 'info', now)
}

/** Finishes setting a route once its points are proved in position: the entrance signal clears. */
export function completeRoute(state: SimState, result: CheckResult, routeId: string, now = new Date()): SimState {
  if (state.routes[routeId] !== 'setting') return state
  const route = routeById(result, routeId)!
  const next: SimState = { ...state, routes: { ...state.routes, [routeId]: 'set' } }
  const locked = Object.keys(route.points)
  const blocked = isBlocked(next, route)
  return addLog(
    next,
    `${routeId} set${locked.length ? `, ${locked.join(', ')} locked` : ''}. ${route.entrance} ${blocked ? 'held at danger: the route is occupied' : 'cleared'}.`,
    blocked ? 'bad' : 'good',
    now,
  )
}

/** Cancels a route: its signal returns to danger and its points are released. */
export function cancelRoute(state: SimState, result: CheckResult, routeId: string, now = new Date()): SimState {
  if (!state.routes[routeId]) return state
  const routes = { ...state.routes }
  delete routes[routeId]
  const route = routeById(result, routeId)
  return addLog({ ...state, routes }, `${routeId} cancelled. ${route?.entrance ?? 'Signal'} at danger, points released.`, 'info', now)
}

/** Occupies or clears a track section (as if a train or vehicle were detected there). */
export function toggleOccupied(state: SimState, result: CheckResult, sectionId: string, now = new Date()): SimState {
  if (state.occupied.includes(sectionId)) {
    return addLog({ ...state, occupied: state.occupied.filter((s) => s !== sectionId) }, `${sectionId} clear.`, 'info', now)
  }
  const next = { ...state, occupied: [...state.occupied, sectionId] }
  const affected = setRoutes(state, result).filter((r) => [...r.sections, ...r.overlap].includes(sectionId))
  const message = affected.length
    ? `${sectionId} occupied. ${affected.map((r) => r.entrance).join(', ')} returned to danger.`
    : `${sectionId} occupied.`
  return addLog(next, message, affected.length ? 'bad' : 'info', now)
}

function isBlocked(state: SimState, route: Route): boolean {
  return [...route.sections, ...route.overlap].some((s) => state.occupied.includes(s))
}

/** What a signal shows: clear only when a route from it is fully set and its route and overlap are clear. */
export function signalAspect(state: SimState, result: CheckResult, signalId: string): Aspect {
  const clear = setRoutes(state, result).some(
    (r) => r.entrance === signalId && state.routes[r.id] === 'set' && !isBlocked(state, r),
  )
  return clear ? 'clear' : 'danger'
}
