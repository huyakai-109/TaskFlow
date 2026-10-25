import { createBrowserRouter, Navigate } from 'react-router-dom'

import { AppLayout } from './app/AppLayout'
import { BoardPage } from './features/board/BoardPage'

/**
 * Routes are declared in one place. Auth guards come in with the login screen —
 * and are presentation only: the API enforces every permission itself.
 */
export const router = createBrowserRouter([
  {
    path: '/',
    element: <AppLayout />,
    children: [
      { index: true, element: <Navigate to="/board" replace /> },
      { path: 'board', element: <BoardPage /> },
    ],
  },
])
