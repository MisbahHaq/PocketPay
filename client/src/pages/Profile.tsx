import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";

interface Permission {
  Key: string;
  Description: string;
}

const statusClass = (s: string) =>
  s === "Active" ? "badge badge-mint" : s === "Suspended" ? "badge badge-coral" : "badge badge-muted";

export default function Profile() {
  const { user, has } = useAuth();
  const [catalog, setCatalog] = useState<Permission[]>([]);

  useEffect(() => {
    if (has("manage_roles")) {
      api
        .get<Permission[]>("/permissions")
        .then((res) => setCatalog(res.data))
        .catch(() => setCatalog([]));
    }
  }, [has]);

  if (!user) return <div className="spinner" />;

  const describe = (key: string) =>
    catalog.find((p) => p.Key === key)?.Description ?? "";

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Account</h1>
          <p className="subtitle">Your profile, organization and permissions.</p>
        </div>
        <span className={statusClass(user.status)}>{user.status}</span>
      </div>

      <div className="grid grid-2">
        <div className="card">
          <div className="section-title">Profile</div>
          <div className="profile-row">
            <span className="muted">Username</span>
            <span className="cell-strong">{user.userName}</span>
          </div>
          <div className="profile-row">
            <span className="muted">Email</span>
            <span className="cell-strong">{user.userEmail}</span>
          </div>
          <div className="profile-row">
            <span className="muted">Status</span>
            <span className={statusClass(user.status)}>{user.status}</span>
          </div>
        </div>

        <div className="card">
          <div className="section-title">Organization</div>
          {user.organization ? (
            <>
              <div className="profile-row">
                <span className="muted">Name</span>
                <span className="cell-strong">{user.organization.name}</span>
              </div>
              <div className="profile-row">
                <span className="muted">Role</span>
                <span>
                  {user.roles.length ? (
                    user.roles.map((r) => (
                      <span className="badge badge-yellow" key={r}>
                        {r}
                      </span>
                    ))
                  ) : (
                    <span className="muted">No role assigned</span>
                  )}
                </span>
              </div>
              {(has("manage_users") || has("invite_users")) && (
                <Link to="/members" className="btn btn-sm btn-outline mt-16">
                  Manage members
                </Link>
              )}
            </>
          ) : (
            <>
              <p className="muted">You don't belong to an organization yet.</p>
              <Link to="/org/new" className="btn btn-primary btn-sm mt-16">
                Create an organization
              </Link>
            </>
          )}
        </div>
      </div>

      <div className="card mt-16">
        <div className="section-title">
          Your permissions <span className="badge badge-muted">{user.permissions.length}</span>
        </div>
        {user.permissions.length === 0 ? (
          <p className="muted">
            None yet — {user.organization ? "ask an administrator for a role." : "join or create an organization first."}
          </p>
        ) : (
          <div className="perm-grid">
            {user.permissions.map((p) => (
              <div className="perm-check on" key={p}>
                <span className="perm-key mono">{p}</span>
                <span className="perm-desc">{describe(p)}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </Layout>
  );
}
