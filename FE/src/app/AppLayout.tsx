import { Outlet } from 'react-router-dom'

import styles from './AppLayout.module.css'

export function AppLayout() {
  return (
    <div className={styles.shell}>
      <header className={styles.header}>
        <h1 className={styles.title}>TaskFlow</h1>
      </header>
      <main className={styles.main}>
        <Outlet />
      </main>
    </div>
  )
}
