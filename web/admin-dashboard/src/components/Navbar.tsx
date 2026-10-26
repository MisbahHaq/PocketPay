import { getSessionUser, isAdmin } from "../auth/guard";
import { tokenStore } from "../auth/tokenStore";
import { isDemoMode, setDemoMode } from "../api/client";

interface NavbarProps {
  currentPath: string;
  onNavigate: (path: string) => void;
  onOpenLogin: () => void;
  onRefreshSession: () => void;
}

export function Navbar({
  currentPath,
  onNavigate,
  onOpenLogin,
  onRefreshSession,
}: NavbarProps) {
  const user = getSessionUser();
  const authenticatedAsAdmin = isAdmin();
  const demoActive = isDemoMode();

  const navLinks = [
    { label: "Dashboard", path: "" },
    { label: "Users", path: "users" },
    { label: "Transfers", path: "transfers" },
    { label: "Reconciliation", path: "reconciliation" },
    { label: "Audit Log", path: "audit" },
  ];

  const handleLogout = () => {
    tokenStore.clear();
  };

  const toggleDemoMode = () => {
    setDemoMode(!demoActive);
    window.location.reload();
  };

  return (
    <header className="navbar-container">
      <div className="navbar-inner">
        <div className="navbar-brand-section">
          <div className="brand-logo" onClick={() => onNavigate("")} role="button">
            <span className="brand-icon">PK</span>
            <div className="brand-text">
              <span className="brand-title">PayPocket</span>
              <span className="brand-badge">ADMIN</span>
            </div>
          </div>

          <nav className="navbar-links" aria-label="Main Navigation">
            {navLinks.map((item) => {
              const isActive =
                currentPath === item.path ||
                (item.path === "" && (currentPath === "/" || currentPath === ""));
              return (
                <a
                  key={item.label}
                  href={`#/${item.path}`}
                  className={`nav-link ${isActive ? "nav-link-active" : ""}`}
                >
                  {item.label}
                </a>
              );
            })}
          </nav>
        </div>

        <div className="navbar-user-section">
          <button
            onClick={toggleDemoMode}
            className={`badge-mode-toggle ${demoActive ? "mode-demo" : "mode-live"}`}
            title="Toggle between Live API and Offline Simulated Mode"
          >
            {demoActive ? "Demo Mode" : "Live API"}
          </button>

          {authenticatedAsAdmin && user ? (
            <div className="user-profile-menu">
              <div className="user-info-text">
                <span className="user-email">{user.email}</span>
                <span className="badge badge-info badge-xs">Admin</span>
              </div>
              <button
                onClick={onRefreshSession}
                className="btn btn-ghost btn-sm"
                title="Rotate / Refresh Token"
              >
                Rotate Token
              </button>
              <button
                onClick={handleLogout}
                className="btn btn-outline btn-sm"
                title="Sign out"
              >
                Sign out
              </button>
            </div>
          ) : (
            <button
              onClick={onOpenLogin}
              className="btn btn-primary btn-sm"
              title="Admin Login"
            >
              Sign In
            </button>
          )}
        </div>
      </div>
    </header>
  );
}
