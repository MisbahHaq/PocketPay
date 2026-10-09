import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface Summary {
  totalProducts: number;
  totalWarehouses: number;
  lowStock: number;
  pendingPO: number;
  pendingSO: number;
}

export default function Dashboard() {
  const { user, has } = useAuth();
  const [summary, setSummary] = useState<Summary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      try {
        const res = await api.get<Summary>("/reports/summary");
        setSummary(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load dashboard"));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const roles = user?.roles ?? [];

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Dashboard</h1>
          <p className="subtitle">Welcome back, {user?.userName}. Here's your inventory at a glance.</p>
        </div>
        {has("view_products") && (
          <Link to="/products" className="btn btn-primary">
            View products
          </Link>
        )}
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="section-title">Overview</div>
      <div className="grid grid-3">
        <div className="card stat-card">
          <div className="stat-label">Products</div>
          <div className="stat-value">{loading ? "--" : summary?.totalProducts ?? 0}</div>
          <div className="stat-hint">Active catalog items</div>
        </div>
        <div className="card stat-card">
          <div className="stat-label">Warehouses</div>
          <div className="stat-value">{loading ? "--" : summary?.totalWarehouses ?? 0}</div>
          <div className="stat-hint">Storage locations</div>
        </div>
        <div className="card stat-card">
          <div className="stat-label">Low stock</div>
          <div className="stat-value">{loading ? "--" : summary?.lowStock ?? 0}</div>
          <div className="stat-hint">At or below reorder level</div>
        </div>
        <div className="card stat-card">
          <div className="stat-label">Pending POs</div>
          <div className="stat-value">{loading ? "--" : summary?.pendingPO ?? 0}</div>
          <div className="stat-hint">Submitted or approved</div>
        </div>
        <div className="card stat-card">
          <div className="stat-label">Open Sales Orders</div>
          <div className="stat-value">{loading ? "--" : summary?.pendingSO ?? 0}</div>
          <div className="stat-hint">Confirmed or partially fulfilled</div>
        </div>
        <div className="card stat-card">
          <div className="stat-label">Your role</div>
          <div className="stat-value">
            {roles.length ? (
              roles.map((r, i) => (
                <span className={`badge badge-${["yellow", "sky", "mint", "coral"][i % 4]}`} key={r} style={{ marginRight: 6 }}>
                  {r}
                </span>
              ))
            ) : (
              <span className="muted">No role</span>
            )}
          </div>
          <div className="stat-hint">{user?.organization?.name ?? "No organization"}</div>
        </div>
      </div>
    </Layout>
  );
}
