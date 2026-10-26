import { Dashboard } from './pages/Dashboard'
import { Users } from './pages/Users'
import { Transfers } from './pages/Transfers'
import { Reconciliation } from './pages/Reconciliation'
import { Audit } from './pages/Audit'

export default function App() {
  return (
    <div>
      <nav>
        <a href="#/">Dashboard</a> | <a href="#/users">Users</a> | <a href="#/transfers">Transfers</a> | <a href="#/reconciliation">Reconciliation</a> | <a href="#/audit">Audit</a>
      </nav>
      <main>
        {window.location.hash === '#/users' && <Users />}
        {window.location.hash === '#/transfers' && <Transfers />}
        {window.location.hash === '#/reconciliation' && <Reconciliation />}
        {window.location.hash === '#/audit' && <Audit />}
        {(window.location.hash === '' || window.location.hash === '#/' || window.location.hash === '#') && <Dashboard />}
      </main>
    </div>
  )
}
