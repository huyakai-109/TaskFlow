import { request } from './client'
import type { Board, ResultRes } from './types'

/**
 * `GET /tasks/board` (`docs/features/f01-workflow.md` T5 #3). Always returns all
 * five columns, in lifecycle order, even when empty — the client never has to
 * invent a missing column.
 */
export async function getBoard(signal?: AbortSignal): Promise<Board> {
  const envelope = await request<ResultRes<Board>>('/tasks/board', { signal })
  return envelope.result ?? { columns: [] }
}
