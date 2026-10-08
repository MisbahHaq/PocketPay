import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import api from "../services/api";
import Layout from "../components/Layout";
import { getErrorMessage } from "../utils/errors";

interface Project {
  ProjectId: number;
  ProjectName: string;
  Description: string;
  CreatorName: string;
}

export default function ProjectDetail() {
  const { id } = useParams();
  const [project, setProject] = useState<Project | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

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
  }, [id]);

  return (
    <Layout>
      <Link to="/projects" className="back-link">
        &larr; Back to projects
      </Link>

      {loading ? (
        <div className="spinner" />
      ) : error ? (
        <div className="alert alert-error">{error}</div>
      ) : project && (
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
      )}
    </Layout>
  );
}
