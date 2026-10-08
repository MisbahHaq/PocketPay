import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
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

interface ProjectMember {
  UserId: number;
  UserName: string;
  Role: string;
  Status: string;
  JoinedOn: string;
}

interface Assignable {
  UserId: number;
  UserName: string;
  RoleName: string;
}

export default function ProjectDetail() {
  const { id } = useParams();
  const { has } = useAuth();
  const canEdit = has("edit_project");

  const [project, setProject] = useState<Project | null>(null);
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [assignable, setAssignable] = useState<Assignable[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [pickUserId, setPickUserId] = useState(0);
  const [busy, setBusy] = useState(false);

  const loadMembers = useCallback(async () => {
    try {
      const res = await api.get<ProjectMember[]>(`/projects/${id}/members`);
      setMembers(res.data);
    } catch {
      /* members list is secondary — ignore */
    }
  }, [id]);

  useEffect(() => {
    const fetchProject = async () => {
      try {
        const res = await api.get<Project>(`/projects/${id}`);
        setProject(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load project"));
      } finally {
        setLoading(false);
      }
    };
    fetchProject();
    loadMembers();
    if (canEdit) {
      api
        .get<Assignable[]>("/members/assignable")
        .then((res) => {
          setAssignable(res.data);
          if (res.data.length) setPickUserId(res.data[0].UserId);
        })
        .catch(() => setAssignable([]));
    }
  }, [id, canEdit, loadMembers]);

  const flash = (msg: string) => {
    setError("");
    setSuccess(msg);
    setTimeout(() => setSuccess(""), 4000);
  };

  const addMember = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!pickUserId) return;
    setBusy(true);
    try {
      await api.post(`/projects/${id}/members`, { UserId: pickUserId });
      await loadMembers();
      flash("Member added.");
    } catch (err) {
      setError(getErrorMessage(err, "Could not add the member"));
    } finally {
      setBusy(false);
    }
  };

  const removeMember = async (userId: number) => {
    if (!window.confirm("Remove this person from the project?")) return;
    try {
      await api.delete(`/projects/${id}/members/${userId}`);
      await loadMembers();
      flash("Member removed.");
    } catch (err) {
      setError(getErrorMessage(err, "Could not remove the member"));
    }
  };

  const addable = assignable.filter((a) => !members.some((m) => m.UserId === a.UserId));

  return (
    <Layout>
      <Link to="/projects" className="back-link">
        &larr; Back to projects
      </Link>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      {loading ? (
        <div className="spinner" />
      ) : error && !project ? (
        <div className="alert alert-error">{error}</div>
      ) : project && (
        <>
          <div className="card" style={{ padding: 28 }}>
            <div className="page-header" style={{ marginBottom: 16 }}>
              <div>
                <h1>{project.ProjectName}</h1>
                <p className="subtitle">Created by {project.CreatorName}</p>
              </div>
              <span className="badge">#{project.ProjectId}</span>
            </div>
            <p style={{ fontSize: 15.5, lineHeight: 1.6 }}>
              {project.Description || <span className="muted">No description provided.</span>}
            </p>
          </div>

          <div className="card mt-16">
            <div className="section-title">
              Project members <span className="badge badge-muted">{members.length}</span>
            </div>

            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>Member</th>
                    <th>Project role</th>
                    <th>Joined</th>
                    {canEdit && <th style={{ textAlign: "right" }}>Actions</th>}
                  </tr>
                </thead>
                <tbody>
                  {members.length === 0 && (
                    <tr>
                      <td colSpan={canEdit ? 4 : 3} className="muted center">
                        No members yet.
                      </td>
                    </tr>
                  )}
                  {members.map((m) => (
                    <tr key={m.UserId}>
                      <td className="cell-strong">{m.UserName}</td>
                      <td>
                        <span className="badge badge-sky">{m.Role}</span>
                      </td>
                      <td className="mono">{new Date(m.JoinedOn).toLocaleDateString()}</td>
                      {canEdit && (
                        <td style={{ textAlign: "right" }}>
                          <button
                            className="btn btn-sm btn-danger"
                            onClick={() => removeMember(m.UserId)}
                          >
                            Remove
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {canEdit && addable.length > 0 && (
              <form className="action-row mt-16" onSubmit={addMember}>
                <select
                  className="select"
                  value={pickUserId}
                  onChange={(e) => setPickUserId(Number(e.target.value))}
                >
                  {addable.map((a) => (
                    <option key={a.UserId} value={a.UserId}>
                      {a.UserName} {a.RoleName ? `· ${a.RoleName}` : ""}
                    </option>
                  ))}
                </select>
                <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>
                  {busy ? "Adding..." : "Add to project"}
                </button>
              </form>
            )}
          </div>
        </>
      )}
    </Layout>
  );
}
