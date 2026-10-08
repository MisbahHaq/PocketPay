import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import Layout from "../components/Layout";

export default function Dashboard() {
  const { user } = useAuth();
  const roles = user?.roles ?? [];
  const canCreate = roles.includes("Manager") || roles.includes("Admin");

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Welcome back, {user?.userName || "User"}</h1>
          <p className="subtitle">Here's a quick look at your account.</p>
        </div>
        <Link to="/projects" className="btn btn-primary">
          Go to Projects
        </Link>
      </div>

      <div className="section-title">Overview</div>
      <div className="grid grid-3">
        <div className="card stat-card">
          <div className="stat-label">Signed in as</div>
          <div className="stat-value">{user?.userName}</div>
          <div className="stat-hint">{user?.userEmail}</div>
        </div>

        <div className="card stat-card">
          <div className="stat-label">Role</div>
          <div className="stat-value">
            {roles.length ? (
              roles.map((r) => (
                <span className="badge" key={r} style={{ marginRight: 6 }}>
                  {r}
                </span>
              ))
            ) : (
              <span className="muted">No role</span>
            )}
          </div>
          <div className="stat-hint">
            {canCreate ? "You can create projects." : "You can view projects."}
          </div>
        </div>

        <div className="card stat-card">
          <div className="stat-label">Projects</div>
          <div className="stat-value">Manage</div>
          <div className="stat-hint">
            <Link to="/projects">View all projects &rarr;</Link>
          </div>
        </div>
      </div>

      <div className="section-title mt-24">Quick actions</div>
      <div className="grid grid-2">
        <div className="card">
          <h3 className="mt-0">Projects</h3>
          <p className="muted" style={{ marginTop: 6 }}>
            {canCreate
              ? "Create new projects and track existing ones."
              : "Browse projects you have access to."}
          </p>
          <Link to="/projects" className="btn btn-outline mt-16">
            Open projects
          </Link>
        </div>
        <div className="card">
          <h3 className="mt-0">Switch account</h3>
          <p className="muted" style={{ marginTop: 6 }}>
            Log out and sign in with a different user.
          </p>
          <Link to="/login" className="btn btn-outline mt-16">
            Sign in as someone else
          </Link>
        </div>
      </div>
    </Layout>
  );
}
