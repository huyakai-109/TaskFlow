import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './AppLayout'
import { RequireAuth } from './RequireAuth'
import { BoardPage } from '../features/board/BoardPage'
import { LoginPage } from '../features/auth/LoginPage'
import { MyTasksPage } from '../features/my-tasks/MyTasksPage'
import { NotFoundPage } from '../features/not-found/NotFoundPage'
import { OverdueListPage } from '../features/overdue/OverdueListPage'
import { TaskDetailPage } from '../features/task-detail/TaskDetailPage'
import { TeamCapacityPage } from '../features/team-capacity/TeamCapacityPage'

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
          { path: 'tasks/:id', element: <TaskDetailPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
