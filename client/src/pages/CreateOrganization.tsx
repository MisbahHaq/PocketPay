import { useState } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import api from "../services/api";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

export default function CreateOrganization() {
  const { user, refreshMe } = useAuth();
  const navigate = useNavigate();
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [workspace, setWorkspace] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  if (user?.organization) return <Navigate to="/dashboard" replace />;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    setSubmitting(true);
    try {
      await api.post("/organizations", {
        Name: name,
        Description: description || null,
        WorkspaceName: workspace || name,
      });
      await refreshMe();
      navigate("/dashboard", { replace: true });
    } catch (err) {
      setError(getErrorMessage(err, "Could not create the organization"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="auth-shell">
      <div className="auth-card">
        <div className="auth-brand">
          <span className="brand-mark">&#9670;</span> Inventory
        </div>
        <h1>Set up your organization</h1>
        <p className="subtitle">
          Welcome, {user?.userName}. An organization is your team's workspace — you will be its
          administrator and can invite everyone else.
        </p>

        <form onSubmit={handleSubmit}>
          <div className="field">
            <label htmlFor="orgname">Organization name</label>
            <input
              id="orgname"
              className="input"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Acme Inc."
              autoFocus
              required
            />
          </div>
          <div className="field">
            <label htmlFor="wsname">Workspace name</label>
            <input
              id="wsname"
              className="input"
              value={workspace}
              onChange={(e) => setWorkspace(e.target.value)}
              placeholder="Defaults to the organization name"
            />
          </div>
          <div className="field">
            <label htmlFor="orgdesc">Description</label>
            <textarea
              id="orgdesc"
              className="textarea"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="What does your team work on?"
              rows={3}
            />
          </div>

          {error && <div className="alert alert-error">{error}</div>}

          <button type="submit" className="btn btn-primary btn-block" disabled={submitting}>
            {submitting ? "Creating..." : "Create organization"}
          </button>
        </form>
      </div>
    </div>
  );
}
