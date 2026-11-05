import type { PaginationResultRes, TaskSummary } from '../../../api/types'

/**
 * Board page 1 plus any extra pages loaded via "show more", de-duplicated by
 * `id` (first occurrence wins) — needed because a page boundary can repeat a
 * row when data changes between two reads of the same column (F03 "Technical
 * contract" D5/AC14 note); every mutation also refetches both queries.
 */
export function mergeColumnItems(
  pageOneItems: TaskSummary[],
  extraPages: PaginationResultRes<TaskSummary[]>[] | undefined,
): TaskSummary[] {
  const merged = [...pageOneItems]
  const seen = new Set(pageOneItems.map((item) => item.id))

  for (const page of extraPages ?? []) {
    for (const item of page.result ?? []) {
      if (!seen.has(item.id)) {
        seen.add(item.id)
        merged.push(item)
      }
    }
  }

  return merged
}
