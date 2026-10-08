import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import api from "../services/api";

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
    const fetch = async () => {
      try {
        const res = await api.get<Project>(`/projects/${id}`);
        setProject(res.data);
      } catch (err: any) {
        setError(err.response?.data || "Failed");
      } finally {
        setLoading(false);
      }
    };
    fetch();
  }, [id]);

  return (
    <div style={{ padding: 20 }}>
      {loading ? <div>Loading...</div> : error ? <div style={{ color: "red" }}>{error}</div> : project && (
        <div>
          <h2>{project.ProjectName}</h2>
          <p>{project.Description}</p>
          <p>Created by: {project.CreatorName}</p>
        </div>
      )}
    </div>
  );
}