import { useAuth } from '../../context/AuthContext'
import Topbar from './Topbar'
import './AppShell.css'

export default function AppShell({ title, children }) {
  const { user } = useAuth()

  return (
    <div className="shell">
      <div className="shell-main">
        <Topbar
          title={title}
          user={user}
        />

        <main className="shell-content">
          {children}
        </main>
      </div>
    </div>
  )
}