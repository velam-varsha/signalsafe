// Mirrors the JSON returned by the SignalSafe API (src/SignalSafe.Api/Contracts/CheckResult.cs).

export type PointPosition = 'Normal' | 'Reverse'
export type Severity = 'Error' | 'Warning'
export type SignalType = 'Main' | 'Shunt'

export interface Section {
  id: string
  endA: string
  endB: string
  lengthM: number
  line: [number, number][] | null
}

export interface Points {
  id: string
  toe: string
  normal: string
  reverse: string
  linkedWith: string | null
}

export interface Signal {
  id: string
  type: SignalType
  atNode: string
  protects: string
}

export interface LayoutView {
  name: string
  sections: Section[]
  points: Points[]
  signals: Signal[]
  hasDiagram: boolean
}

export interface LayoutIssue {
  code: string
  elementId: string
  message: string
}

export interface Route {
  id: string
  entrance: string
  exit: string
  isShunt: boolean
  sections: string[]
  points: Record<string, PointPosition>
  linkedPoints: string[]
  overlap: string[]
  inTables: boolean
}

export interface Conflict {
  routeA: string
  routeB: string
  sharedSections: string[]
  opposingPoints: string[]
  opposing: boolean
}

export interface Finding {
  ruleId: string
  ruleName: string
  severity: Severity
  routeId: string | null
  elements: string[]
  message: string
  source: { sheet: string; row: number; column: string | null } | null
}

export interface RuleSummary {
  id: string
  name: string
  findingCount: number
}

export interface CheckResult {
  layout: LayoutView
  layoutIssues: LayoutIssue[]
  routes: Route[]
  conflicts: Conflict[]
  tables: { fileName: string; rowCount: number } | null
  findings: Finding[]
  rules: RuleSummary[]
  durationMs: number
}

export interface SampleInfo {
  id: string
  name: string
  description: string
}
