import { useMutation } from '@tanstack/react-query'

import { exportReport } from '../../../api/reports'
import type { ExportReportParams } from '../../../api/reports'

/**
 * `GET /reports/export` (§11.8) — saved through an object URL with the
 * server's own filename, revoked right after the click so it doesn't leak.
 * Busy state (`isPending`) drives the Export button; errors go through the
 * same `errorMessages.ts` copy as every other call.
 */
export function useReportExport() {
  return useMutation({
    mutationFn: (params: ExportReportParams) => exportReport(params),
    onSuccess: ({ blob, filename }) => {
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    },
  })
}
