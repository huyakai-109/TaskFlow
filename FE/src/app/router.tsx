import { lazy, Suspense } from 'react'
import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { ReportsIndexRedirect } from './ReportsIndexRedirect'
import { RequireAuth } from './RequireAuth'
import { RequirePermission } from './RequirePermission'
import { AccountsPage } from '../features/accounts/AccountsPage'
import { BoardPage } from '../features/board/BoardPage'
import { ReportsSkeleton } from '../features/reports/components/ReportsSkeleton'
import { ReportsErrorBoundary } from '../features/reports/ReportsErrorBoundary'
import { LoginPage } from '../features/auth/LoginPage'
import { MyTasksPage } from '../features/my-tasks/MyTasksPage'
import { NotFoundPage } from '../features/not-found/NotFoundPage'
import { OverdueListPage } from '../features/overdue/OverdueListPage'
import { ProfilePage } from '../features/profile/ProfilePage'
import { TaskDetailPage } from '../features/task-detail/TaskDetailPage'
import { TeamCapacityPage } from '../features/team-capacity/TeamCapacityPage'

/**
 * Lazy (`React.lazy`) so `recharts` never enters the main chunk (§11.1) — the
 * only lazy-loaded route in the app; every other page above is a deliberate,
 * eager import (`frontend.md` §10 "lazy-load routes where the project does" —
 * this is the first place it does).
 */
const ReportsPage = lazy(() => import('../features/reports/ReportsPage').then((m) => ({ default: m.ReportsPage })))

/**
 * Routes are declared in one place. `/login` is the only public route; the
 * rest is nested under `RequireAuth` (`docs/features/f01-login.md` AC6–9) —
 * a guard, presentation only, since the API enforces every permission itself.
 */
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    path: '/',
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <Navigate to="/board" replace /> },
          { path: 'board', element: <BoardPage /> },
          { path: 'my-tasks', element: <MyTasksPage /> },
          { path: 'overdue', element: <OverdueListPage /> },
          { path: 'team-capacity', element: <TeamCapacityPage /> },
          { path: 'profile', element: <ProfilePage /> },
          {
            path: 'accounts',
            element: (
              <RequirePermission permission="ManageUsers">
                <AccountsPage />
              </RequirePermission>
            ),
          },
          // F09 Reports (`f09-reports.md` §11.1): `/reports` redirects by
          // permission, `/reports/me` is open to everyone, `/reports/team`
          // and `/reports/members/:userId` are gated — `/activity` is gone,
          // with no redirect (falls to `*`, the Inbox precedent, Q5).
          { path: 'reports', element: <ReportsIndexRedirect /> },
          {
            path: 'reports/me',
            element: (
              <Suspense fallback={<ReportsSkeleton />}>
                <ReportsPage />
              </Suspense>
            ),
            errorElement: <ReportsErrorBoundary />,
          },
          {
            path: 'reports/team',
            element: (
              <RequirePermission permission="ViewReports">
                <Suspense fallback={<ReportsSkeleton />}>
                  <ReportsPage />
                </Suspense>
              </RequirePermission>
            ),
            errorElement: <ReportsErrorBoundary />,
          },
          {
            path: 'reports/members/:userId',
            element: (
              <RequirePermission permission="ViewReports">
                <Suspense fallback={<ReportsSkeleton />}>
                  <ReportsPage />
                </Suspense>
              </RequirePermission>
            ),
            errorElement: <ReportsErrorBoundary />,
          },
          { path: 'tasks/:id', element: <TaskDetailPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
