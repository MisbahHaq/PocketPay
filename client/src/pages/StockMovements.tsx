import { useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { getErrorMessage } from "../utils/errors";

export default function StockMovements() {
  const [data, setData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  useEffect(() => {
    (async () => {
      try {
        const res = await api.get("/inventory/movements", { params: { page: 1, pageSize: 50 } });
        setData(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load movements"));
      } finally {
        setLoading(false);
      }
    })();
  }, []);
  return (
    <Layout>
      <div className="page-header"><h1>Stock Movements</h1></div>
      {error && <div className="alert alert-error">{error}</div>}
      {loading ? <div className="spinner" /> : (
        <div className="card table-wrap">
          <table className="table">
            <thead><tr><th>When</th><th>SKU</th><th>Product</th><th>Warehouse</th><th>Type</th><th>Ref</th><th>Qty</th></tr></thead>
            <tbody>{(data?.items || []).map((m:any) => (<tr key={m.StockMovementId}><td className="mono">{new Date(m.MovedAt).toLocaleString()}</td><td className="mono">{m.SKU}</td><td>{m.ProductName}</td><td>{m.WarehouseName}</td><td>{m.MovementType}</td><td className="mono">{m.ReferenceType} {m.ReferenceId}</td><td>{m.Quantity}</td></tr>))}</tbody>
          </table>
        </div>
      )}
    </Layout>
  );
}
