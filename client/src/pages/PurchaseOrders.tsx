import { useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { getErrorMessage } from "../utils/errors";

interface PO {
  POId: number;
  PONumber: string;
  SupplierName: string;
  OrderDate: string;
  ExpectedDate: string;
  Status: string;
  TotalAmount: number;
}

export default function PurchaseOrders() {
  const [pos, setPos] = useState<PO[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  useEffect(() => {
    (async () => {
      try {
        const res = await api.get<PO[]>("/purchaseorders");
        setPos(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load POs"));
      } finally {
        setLoading(false);
      }
    })();
  }, []);
  return (
    <Layout>
      <div className="page-header"><h1>Purchase Orders</h1><button className="btn btn-primary">+ New PO</button></div>
      {error && <div className="alert alert-error">{error}</div>}
      {loading ? <div className="spinner" /> : (
        <div className="card table-wrap">
          <table className="table">
            <thead><tr><th>PO#</th><th>Supplier</th><th>Date</th><th>Expected</th><th>Status</th><th>Total</th></tr></thead>
            <tbody>{pos.map(p => (<tr key={p.POId}><td className="mono">{p.PONumber}</td><td>{p.SupplierName}</td><td>{new Date(p.OrderDate).toLocaleDateString()}</td><td>{p.ExpectedDate ? new Date(p.ExpectedDate).toLocaleDateString() : "-"}</td><td>{p.Status}</td><td>{p.TotalAmount}</td></tr>))}</tbody>
          </table>
        </div>
      )}
    </Layout>
  );
}
