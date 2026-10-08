import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface Project {
  ProjectId: number;
  ProjectName: string;
  Description: string;
  CreatorName: string;
}

export default function Projects() {
  const { user } = useAuth();
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [name, setName] = useState("");
  const [desc, setDesc] = useState("");
  const [showForm, setShowForm] = useState(false);
  const [creating, setCreating] = useState(false);

  const roles = user?.roles ?? [];
  const canCreate = roles.includes("Manager") || roles.includes("Admin");

  const fetchProjects = async () => {
    try {
      const res = await api.get<Project[]>("/projects/all");
      setProjects(res.data);
      setError("");
    } catch (err) {
      setError(getErrorMessage(err, "Failed to load projects"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchProjects();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    setError("");
    setSuccess("");
    try {
      await api.post("/projects/create", { ProjectName: name, Description: desc });
      setName("");
      setDesc("");
      setShowForm(false);
      setSuccess("Project created.");
      fetchProjects();
    } catch (err) {
      setError(getErrorMessage(err, "Failed to create project"));
    } finally {
      setCreating(false);
    }
  };

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Projects</h1>
          <p className="subtitle">
            {projects.length} {projects.length === 1 ? "project" : "projects"} in your workspace.
          </p>
        </div>
        {canCreate && (
          <button className="btn btn-primary" onClick={() => setShowForm((s) => !s)}>
            {showForm ? "Close" : "+ New project"}
          </button>
        )}
      </div>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      {showForm && (
        <form className="card mb-16" onSubmit={handleCreate}>
          <div className="section-title">New project</div>
          <div className="form-row">
            <div className="field">
              <label htmlFor="pname">Project name</label>
              <input
                id="pname"
                className="input"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. Website Redesign"
                required
              />
            </div>
            <div className="field">
              <label htmlFor="pdesc">Description</label>
              <input
                id="pdesc"
                className="input"
                value={desc}
                onChange={(e) => setDesc(e.target.value)}
                placeholder="What is this project about?"
              />
            </div>
          </div>
          <button type="submit" className="btn btn-primary" disabled={creating}>
            {creating ? "Creating..." : "Create project"}
          </button>
        </form>
      )}

      {loading ? (
        <div className="spinner" />
      ) : projects.length === 0 ? (
        <div className="state">
          <h3>No projects yet</h3>
          <p>
            {canCreate
              ? "Create your first project to get started."
              : "A Manager or Admin needs to create a project first."}
          </p>
          {canCreate && !showForm && (
            <button className="btn btn-primary" onClick={() => setShowForm(true)}>
              Create project
            </button>
          )}
        </div>
      ) : (
        <div className="grid grid-3">
          {projects.map((p) => (
            <div className="card project-card" key={p.ProjectId}>
              <h3>
                <Link className="stretched" to={`/projects/${p.ProjectId}`}>
                  {p.ProjectName}
                </Link>
              </h3>
              <p className="project-desc">{p.Description || "No description"}</p>
              <div className="project-meta">
                <span>Created by {p.CreatorName}</span>
                <span className="badge badge-muted">#{p.ProjectId}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </Layout>
  );
}
