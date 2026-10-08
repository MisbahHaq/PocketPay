import { useAuth } from "../context/AuthContext";
import { Link } from "react-router-dom";

export default function Dashboard() {
  const { user, logout } = useAuth();
  return (
    <div style={{ padding: 20 }}>
      <h2>Dashboard</h2>
      <p>Welcome {user?.userName || "User"}</p>
      <button onClick={logout}>Logout</button>
      <div style={{ marginTop: 20 }}>
        <Link to="/projects">Manage Projects</Link>
      </div>
    </div>
  );
}