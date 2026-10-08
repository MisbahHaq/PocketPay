import { Link, NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function Layout({ children }: { children: React.ReactNode }) {
  const { user, logout } = useAuth();
  const role = user?.roles?.[0];
  const initial = (user?.userName || "?").charAt(0);

  return (
    <div className="app-shell">
      <header className="navbar">
        <div className="navbar-inner">
          <Link to="/dashboard" className="brand">
            <span className="brand-mark">&#9670;</span> Inventory
          </Link>

          <nav className="nav-links">
            <NavLink to="/dashboard" className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}>
              Dashboard
            </NavLink>
            <NavLink to="/projects" className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}>
              Projects
            </NavLink>
          </nav>

          <div className="nav-right">
            <span className="user-chip">
              <span className="avatar">{initial}</span>
              <span className="chip-name">
                {user?.userName}
                {role && <> &middot; <span className="badge">{role}</span></>}
              </span>
            </span>
            <button className="btn btn-ghost btn-sm" onClick={logout}>
              Logout
            </button>
          </div>
        </div>
      </header>

      <main className="page">{children}</main>
    </div>
  );
}
