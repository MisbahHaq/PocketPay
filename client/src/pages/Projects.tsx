import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../services/api";

interface Project {
  ProjectId: number;
  ProjectName: string;
  Description: string;
  CreatorName: string;
}

export default function Projects() {
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [name, setName] = useState("");
  const [desc, setDesc] = useState("");

  const fetchProjects = async () => {
    try {
      const res = await api.get<Project[]>("/projects/all");
      setProjects(res.data);
    } catch (err: any) {
      setError(err.response?.data || "Failed to fetch");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchProjects();
  }, []);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await api.post("/projects/create", { ProjectName: name, Description: desc });
      setName("");
      setDesc("");
      fetchProjects();
    } catch (err: any) {
      setError(err.response?.data || "Failed to create");
    }
  };

  return (
    <div style={{ padding: 20 }}>
      <h2>Projects</h2>
      <form onSubmit={handleCreate} style={{ marginBottom: 20 }}>
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Project Name" />
        <input value={desc} onChange={(e) => setDesc(e.target.value)} placeholder="Description" style={{ marginLeft: 8 }} />
        <button type="submit" style={{ marginLeft: 8 }}>Create (Manager)</button>
      </form>
      {loading ? <div>Loading...</div> : error ? <div style={{ color: "red" }}>{error}</div> : (
        <ul>
          {projects.map((p) => (
            <li key={p.ProjectId} style={{ marginBottom: 8 }}>
              <Link to={`/projects/${p.ProjectId}`}>{p.ProjectName}</Link> - {p.CreatorName}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}