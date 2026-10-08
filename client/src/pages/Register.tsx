import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export default function Register() {
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const { register } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await register(username, email, password);
      navigate("/login");
    } catch (err: any) {
      setError(err.response?.data || "Register failed");
    }
  };

  return (
    <div style={{ maxWidth: 400, margin: "40px auto", padding: 20 }}>
      <h2>Register</h2>
      <form onSubmit={handleSubmit}>
        <div style={{ marginBottom: 10 }}>
          <input value={username} onChange={(e) => setUsername(e.target.value)} placeholder="Username" style={{ width: "100%", padding: 8 }} />
        </div>
        <div style={{ marginBottom: 10 }}>
          <input value={email} onChange={(e) => setEmail(e.target.value)} placeholder="Email" style={{ width: "100%", padding: 8 }} />
        </div>
        <div style={{ marginBottom: 10 }}>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Password" style={{ width: "100%", padding: 8 }} />
        </div>
        {error && <div style={{ color: "red", marginBottom: 10 }}>{error}</div>}
        <button type="submit" style={{ width: "100%", padding: 8 }}>Register</button>
      </form>
      <p style={{ marginTop: 10 }}>
        Already have an account? <a href="/login">Login</a>
      </p>
    </div>
  );
}