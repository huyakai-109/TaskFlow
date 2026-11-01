import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { describeError, describeErrorCode } from '../../api/errorMessages'
import { MoveRefusalDialog } from '../board/MoveRefusalDialog'
import { MoveToast } from '../board/MoveToast'
import { useMoveTaskMutation } from '../board/useMoveTaskMutation'

import { HistoryTimeline } from './HistoryTimeline'
import { PropertiesPanel } from './PropertiesPanel'
import { ReopenDialog } from './ReopenDialog'
import styles from './TaskDetailPage.module.css'
import { useReopenTaskMutation } from './useReopenTaskMutation'
import { useTaskHistoryQuery } from './useTaskHistoryQuery'
import { useTaskQuery } from './useTaskQuery'
import { WorkflowStrip } from './WorkflowStrip'

const CONFLICT_CODES = new Set(['Task.Move.Conflict', 'Task.Move.NotFound'])

/**
 * The task detail screen (`docs/ui/screens/TaskDetail.dc.html`): workflow
 * strip with every legal move plus the Manager/Admin-only reopen action,
 * read-only activity timeline (F04), and the properties panel. Routed at
 * `/tasks/:id`, reachable from a board card's key or title (`TaskCard`).
 */
export function TaskDetailPage() {
  const { id } = useParams<{ id: string }>()
  const taskId = Number(id)
  const taskQuery = useTaskQuery(taskId)
  const historyQuery = useTaskHistoryQuery(taskId)

  const [refusal, setRefusal] = useState<{ code: string | null; taskTitle: string } | null>(null)
  const [toastMessage, setToastMessage] = useState<string | null>(null)
  const [reopenOpen, setReopenOpen] = useState(false)

  const moveMutation = useMoveTaskMutation({
    onRefused: (code, taskTitle) => {
      if (code !== null && CONFLICT_CODES.has(code)) {
        setToastMessage(describeErrorCode(code))
        return
      }

      setRefusal({ code, taskTitle })
    },
  })

  const reopenMutation = useReopenTaskMutation({
    onRefused: (code) => setToastMessage(describeErrorCode(code)),
  })

  if (taskQuery.isPending) {
    return (
      <p className={styles.note} role="status">
        Loading task…
      </p>
    )
  }

  if (taskQuery.isError) {
    return (
      <div className={styles.errorBox} role="alert">
        <p>{describeError(taskQuery.error)}</p>
        <Link to="/board">Back to board</Link>
      </div>
    )
  }

  const task = taskQuery.data
  const moves = task.allowedTransitions.filter((t) => t.operation === 'Move')
  const reopen = task.allowedTransitions.find((t) => t.operation === 'Reopen')

  return (
    <section className={styles.page}>
      <Link to="/board" className={styles.back}>
        ← Board
      </Link>

      <div className={styles.header}>
        <h1 className={styles.title}>{task.title}</h1>
        {task.description !== null && task.description !== '' && (
          <p className={styles.description}>{task.description}</p>
        )}
      </div>

      <div className={styles.body}>
        <main className={styles.main}>
          <WorkflowStrip
            status={task.status}
            moves={moves}
            reopen={reopen}
            onMove={(toStatus) =>
              moveMutation.mutate({ taskId: task.id, toStatus, version: task.version, taskTitle: task.title })
            }
            onReopenRequested={() => setReopenOpen(true)}
          />

          <section aria-labelledby="activity-heading" className={styles.activity}>
            <div className={styles.activityHeader}>
              <h2 id="activity-heading" className={styles.sectionTitle}>
                Activity
              </h2>
              <span className={styles.permanentNote}>Permanent — entries can&rsquo;t be edited or deleted</span>
            </div>
            {historyQuery.isPending && (
              <p role="status" className={styles.note}>
                Loading history…
              </p>
            )}
            {historyQuery.isError && (
              <p role="alert" className={styles.note}>
                {describeError(historyQuery.error)}
              </p>
            )}
            {historyQuery.isSuccess && <HistoryTimeline entries={historyQuery.data} />}
          </section>
        </main>

        <PropertiesPanel task={task} />
      </div>

      {reopenOpen && (
        <ReopenDialog
          taskTitle={task.title}
          isPending={reopenMutation.isPending}
          onCancel={() => setReopenOpen(false)}
          onSubmit={(reason) => {
            reopenMutation.mutate(
              { taskId: task.id, reason, version: task.version },
              { onSuccess: () => setReopenOpen(false) },
            )
          }}
        />
      )}

      {refusal !== null && (
        <MoveRefusalDialog code={refusal.code} taskTitle={refusal.taskTitle} onClose={() => setRefusal(null)} />
      )}

      {toastMessage !== null && <MoveToast message={toastMessage} onDismiss={() => setToastMessage(null)} />}
    </section>
  )
}
