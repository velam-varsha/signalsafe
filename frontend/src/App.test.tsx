import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import App from './App'
import samples from './test/fixtures/samples.json'
import westbrook from './test/fixtures/westbrook-check.json'

function mockApi(check: unknown = westbrook) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    const body = url.endsWith('/api/v1/samples') ? samples : check
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('App', () => {
  beforeEach(() => {
    mockApi()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the Westbrook sample and shows its findings', async () => {
    render(<App />)

    expect(await screen.findByText('R03 · Point position')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /^R\d\d/ })).toHaveLength(7)
    expect(screen.getByText(/5 routes/)).toBeInTheDocument()
    expect(screen.getByText('westbrook-control-tables.xlsx')).toBeInTheDocument()
  })

  it('highlights the first finding on the diagram', async () => {
    render(<App />)
    await screen.findByText('R03 · Point position')

    // R03 is about P1 on S1(M)-S5: T5 is part of it, T13 is not.
    expect(screen.getByTestId('section-T5')).toHaveAttribute('data-state', 'err')
    expect(screen.getByTestId('section-T13')).toHaveAttribute('data-state', 'faded')
  })

  it('filters findings by severity', async () => {
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('R03 · Point position')

    await user.click(screen.getByRole('button', { name: 'Warnings 2' }))

    const cards = screen.getAllByRole('button', { name: /^R\d\d/ })
    expect(cards).toHaveLength(2)
    expect(within(cards[0]).getByText('Warning')).toBeInTheDocument()
  })

  it('shows only the findings for an element clicked on the diagram', async () => {
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('R03 · Point position')

    await user.click(screen.getByRole('button', { name: 'Track section T12' }))

    expect(screen.getByText(/Showing findings for/)).toBeInTheDocument()
    const cards = screen.getAllByRole('button', { name: /^R\d\d/ })
    expect(cards.map((c) => within(c).getByText(/^R\d\d ·/).textContent)).toEqual([
      'R04 · Track clearance',
      'R07 · Overlap',
      'R10 · Data consistency',
    ])
  })

  it('previews a route from the Routes tab', async () => {
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('R03 · Point position')

    await user.click(screen.getByRole('tab', { name: /Routes/ }))
    await user.click(screen.getByRole('button', { name: /^S1\(M\)-S5/ }))

    expect(screen.getByTestId('section-T5')).toHaveAttribute('data-state', 'prev')
    expect(screen.getByTestId('section-T3')).toHaveAttribute('data-state', 'faded')
    expect(screen.getByTestId('points-P1')).toHaveTextContent('P1 R')
    expect(screen.getByText('Missing')).toBeInTheDocument() // S7(S)-S6 is not in the tables
  })

  it('shows a clear message when the API cannot be reached', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('Failed to fetch') }))
    render(<App />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot reach the SignalSafe API')
  })
})
