import { Outlet } from 'react-router-dom'

import { NotificationBell } from '../features/notifications/components/NotificationBell'

import styles from './AppLayout.module.css'

export function AppLayout() {
  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <h1 className={styles.title}>TaskFlow</h1>
        <NotificationBell />
      </header>
      <main className={styles.main}>
        <Outlet />
      </main>
    </div>
  )
}
