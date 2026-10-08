import { useCallback, useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface Role {
  RoleId: number;
  Name: string;
  Description: string;
  IsSystem: boolean;
  Permissions: string[];
  MemberCount: number;
}

interface Permission {
  Key: string;
  Description: string;
}

export default function Roles() {
  const { has } = useAuth();
  const allowed = has("manage_roles");

  const [roles, setRoles] = useState<Role[]>([]);
  const [catalog, setCatalog] = useState<Permission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [editing, setEditing] = useState<Role | null>(null);
  const [editName, setEditName] = useState("");
  const [editDesc, setEditDesc] = useState("");
  const [editPerms, setEditPerms] = useState<string[]>([]);
  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState("");
  const [newDesc, setNewDesc] = useState("");
  const [newPerms, setNewPerms] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const [r, p] = await Promise.all([api.get<Role[]>("/roles"), api.get<Permission[]>("/permissions")]);
      setRoles(r.data);
      setCatalog(p.data);
    } catch (err) {
      setError(getErrorMessage(err, "Failed to load roles"));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (allowed) load();
    else setLoading(false);
  }, [allowed, load]);

  const flash = (msg: string) => {
    setError("");
    setSuccess(msg);
    setTimeout(() => setSuccess(""), 4000);
  };

  const fail = (err: unknown, fallback: string) => {
    setSuccess("");
    setError(getErrorMessage(err, fallback));
  };

  const startEdit = (role: Role) => {
    setCreating(false);
    setEditing(role);
    setEditName(role.Name);
    setEditDesc(role.Description);
    setEditPerms([...role.Permissions]);
  };

  const toggle = (key: string, list: string[], setList: (v: string[]) => void) => {
    setList(list.includes(key) ? list.filter((k) => k !== key) : [...list, key]);
  };

  const saveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editing) return;
    setBusy(true);
    try {
      await api.put(`/roles/${editing.RoleId}`, {
        Name: editName,
        Description: editDesc,
        Permissions: editPerms,
      });
      setEditing(null);
      await load();
      flash("Role updated.");
    } catch (err) {
      fail(err, "Could not update the role");
    } finally {
      setBusy(false);
    }
  };

  const createRole = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.post("/roles", {
        Name: newName,
        Description: newDesc,
        Permissions: newPerms,
      });
      setCreating(false);
      setNewName("");
      setNewDesc("");
      setNewPerms([]);
      await load();
      flash("Role created.");
    } catch (err) {
      fail(err, "Could not create the role");
    } finally {
      setBusy(false);
    }
  };

  const deleteRole = async (role: Role) => {
    if (!window.confirm(`Delete the "${role.Name}" role?`)) return;
    try {
      await api.delete(`/roles/${role.RoleId}`);
      await load();
      flash("Role deleted.");
    } catch (err) {
      fail(err, "Could not delete the role");
    }
  };

  if (!allowed) {
    return (
      <Layout>
        <div className="state">
          <h3>Not allowed</h3>
          <p>You don't have permission to manage roles.</p>
        </div>
      </Layout>
    );
  }

  const permGrid = (selected: string[], setSelected: (v: string[]) => void) => (
    <div className="perm-grid">
      {catalog.map((p) => {
        const on = selected.includes(p.Key);
        return (
          <label key={p.Key} className={`perm-check${on ? " on" : ""}`}>
            <input
              type="checkbox"
              checked={on}
              onChange={() => toggle(p.Key, selected, setSelected)}
            />
            <span className="perm-key mono">{p.Key}</span>
            <span className="perm-desc">{p.Description}</span>
          </label>
        );
      })}
    </div>
  );

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Roles &amp; permissions</h1>
          <p className="subtitle">
            {roles.length} roles &middot; permissions decide what each role can do.
          </p>
        </div>
        <button
          className="btn btn-primary"
          onClick={() => {
            setEditing(null);
            setCreating((c) => !c);
          }}
        >
          {creating ? "Close" : "+ New role"}
        </button>
      </div>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      {creating && (
        <form className="card mb-16" onSubmit={createRole}>
          <div className="section-title">New role</div>
          <div className="form-row">
            <div className="field">
              <label htmlFor="nr-name">Name</label>
              <input
                id="nr-name"
                className="input"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder="e.g. Support Lead"
                required
              />
            </div>
            <div className="field">
              <label htmlFor="nr-desc">Description</label>
              <input
                id="nr-desc"
                className="input"
                value={newDesc}
                onChange={(e) => setNewDesc(e.target.value)}
                placeholder="What is this role for?"
              />
            </div>
          </div>
          <div className="section-title" style={{ marginTop: 8 }}>Permissions</div>
          {permGrid(newPerms, setNewPerms)}
          <div className="action-row mt-16">
            <button type="submit" className="btn btn-primary" disabled={busy}>
              {busy ? "Creating..." : "Create role"}
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => setCreating(false)}>
              Cancel
            </button>
          </div>
        </form>
      )}

      {editing && (
        <form className="card mb-16" onSubmit={saveEdit}>
          <div className="section-title">
            Editing <span className="badge badge-yellow">{editing.Name}</span>
            {editing.IsSystem && <span className="badge badge-muted">built-in</span>}
          </div>
          <div className="form-row">
            <div className="field">
              <label htmlFor="er-name">Name</label>
              <input
                id="er-name"
                className="input"
                value={editName}
                onChange={(e) => setEditName(e.target.value)}
                disabled={editing.IsSystem}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="er-desc">Description</label>
              <input
                id="er-desc"
                className="input"
                value={editDesc}
                onChange={(e) => setEditDesc(e.target.value)}
              />
            </div>
          </div>
          <div className="section-title" style={{ marginTop: 8 }}>Permissions</div>
          {permGrid(editPerms, setEditPerms)}
          <div className="action-row mt-16">
            <button type="submit" className="btn btn-primary" disabled={busy}>
              {busy ? "Saving..." : "Save changes"}
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => setEditing(null)}>
              Cancel
            </button>
          </div>
        </form>
      )}

      {loading ? (
        <div className="spinner" />
      ) : (
        <div className="grid grid-3">
          {roles.map((r) => (
            <div className="card role-card" key={r.RoleId}>
              <div className="role-card-head">
                <h3>{r.Name}</h3>
                {r.IsSystem ? (
                  <span className="badge badge-sky">built-in</span>
                ) : (
                  <span className="badge badge-yellow">custom</span>
                )}
              </div>
              <p className="muted" style={{ fontSize: 13.5 }}>
                {r.Description || "No description."}
              </p>
              <div className="project-meta">
                <span>{r.MemberCount} member(s)</span>
                <span className="badge badge-muted">{r.Permissions.length} permissions</span>
              </div>
              <div className="action-row mt-16">
                <button className="btn btn-sm btn-outline" onClick={() => startEdit(r)}>
                  Edit permissions
                </button>
                {!r.IsSystem && (
                  <button className="btn btn-sm btn-danger" onClick={() => deleteRole(r)}>
                    Delete
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </Layout>
  );
}
