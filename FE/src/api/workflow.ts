import { request } from './client'
import type { ResultRes, Workflow } from './types'

/** `GET /workflow` (f10-read-models-and-sort.md §4) — the configured in-progress limit, server timezone, "today", and the overdue scan's last/next fire times. */
export async function getWorkflow(signal?: AbortSignal): Promise<Workflow> {
  const envelope = await request<ResultRes<Workflow>>('/workflow', { signal })

  if (envelope.result == null) {
    throw new Error('GET /workflow returned no result')
  }

  return envelope.result
}
