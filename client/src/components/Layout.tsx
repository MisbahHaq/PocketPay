import { Link, NavLink } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function Layout({ children }: { children: React.ReactNode }) {
  const { user, logout, has } = useAuth();
  const role = user?.roles?.[0];
  const initial = (user?.userName || "?").charAt(0);
  const inOrg = !!user?.organization;

  const link = (to: string, label: string) => (
    <NavLink to={to} className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}>
      {label}
    </NavLink>
  );

  return (
    <div className="app-shell">
      <header className="navbar">
        <div className="navbar-inner">
          <Link to="/dashboard" className="brand">
            <span className="brand-mark">&#9670;</span> Inventory
          </Link>

          <nav className="nav-links">
            {link("/dashboard", "Dashboard")}
            {link("/projects", "Projects")}
            {inOrg && (has("manage_users") || has("invite_users")) && link("/members", "Members")}
            {inOrg && has("manage_roles") && link("/roles", "Roles")}
            {inOrg && has("manage_organization") && link("/audit", "Audit")}
          </nav>

          <div className="nav-right">
            <Link to="/profile" className="user-chip" title="Account & profile">
              <span className="avatar">{initial}</span>
              <span className="chip-name">
                {user?.userName}
                {user?.organization && <> &middot; <span className="badge badge-sky">{user.organization.name}</span></>}
                {role && <> &middot; <span className="badge">{role}</span></>}
              </span>
            </Link>
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
