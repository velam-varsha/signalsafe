import type { CheckResult, SampleInfo } from './types'

// Empty in development (Vite proxies /api to the local API). Set VITE_API_URL when the API is hosted elsewhere.
const base = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

/** An error with a message that can be shown to the user as it is. */
export class ApiError extends Error {}

async function send(path: string, init?: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`${base}${path}`, init)
  } catch {
    throw new ApiError('Cannot reach the SignalSafe API. Check that it is running (dotnet run --project src/SignalSafe.Api).')
  }
  if (!response.ok) {
    let message = `The API answered ${response.status}.`
    try {
      const body = await response.json()
      if (typeof body?.error === 'string') message = body.error
    } catch {
      // keep the status message
    }
    throw new ApiError(message)
  }
  return response
}

export async function listSamples(): Promise<SampleInfo[]> {
  return (await send('/api/v1/samples')).json()
}

export async function checkSample(id: string): Promise<CheckResult> {
  return (await send(`/api/v1/samples/${encodeURIComponent(id)}/check`)).json()
}

function form(layout: File, tables?: File | null): FormData {
  const data = new FormData()
  data.append('layout', layout)
  if (tables) data.append('tables', tables)
  return data
}

export async function checkFiles(layout: File, tables?: File | null): Promise<CheckResult> {
  return (await send('/api/v1/check', { method: 'POST', body: form(layout, tables) })).json()
}

export async function downloadReport(layout: File, tables: File): Promise<Blob> {
  return (await send('/api/v1/check/report', { method: 'POST', body: form(layout, tables) })).blob()
}

export async function sampleFile(id: string, kind: 'layout' | 'tables', name: string): Promise<File> {
  const blob = await (await send(`/api/v1/samples/${encodeURIComponent(id)}/files/${kind}`)).blob()
  return new File([blob], name)
}

export async function templateBlob(): Promise<Blob> {
  return (await send('/api/v1/templates/control-tables')).blob()
}

/** Saves a blob as a file in the browser's downloads. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
