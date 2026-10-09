import { useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { getErrorMessage } from "../utils/errors";

export default function SalesOrders() {
  const [so, setSo] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  useEffect(() => {
    (async () => {
      try {
        const res = await api.get("/salesorders");
        setSo(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load SOs"));
      } finally {
        setLoading(false);
      }
    })();
  }, []);
  return (
    <Layout>
      <div className="page-header"><h1>Sales Orders</h1><button className="btn btn-primary">+ New SO</button></div>
      {error && <div className="alert alert-error">{error}</div>}
      {loading ? <div className="spinner" /> : (
        <div className="card table-wrap">
          <table className="table">
            <thead><tr><th>SO#</th><th>Customer</th><th>Date</th><th>Status</th><th>Total</th></tr></thead>
            <tbody>{so.map((s:any) => (<tr key={s.SOId}><td className="mono">{s.SONumber}</td><td>{s.CustomerName}</td><td>{new Date(s.OrderDate).toLocaleDateString()}</td><td>{s.Status}</td><td>{s.TotalAmount}</td></tr>))}</tbody>
          </table>
        </div>
      )}
    </Layout>
  );
}
