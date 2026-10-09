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
            {inOrg && has("view_products") && link("/products", "Products")}
            {inOrg && has("view_stock") && link("/stock", "Stock")}
            {inOrg && has("view_stock") && link("/stock/movements", "Movements")}
            {inOrg && has("view_po") && link("/purchase-orders", "Purchases")}
            {inOrg && has("view_so") && link("/sales-orders", "Sales")}
            {inOrg && (has("view_categories") || has("view_units") || has("view_suppliers") || has("view_customers") || has("view_warehouses")) && link("/masters", "Masters")}
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
