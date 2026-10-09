import { useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { getErrorMessage } from "../utils/errors";

interface Stock {
  WarehouseId: number;
  WarehouseName: string;
  ProductId: number;
  SKU: string;
  ProductName: string;
  QuantityOnHand: number;
  ReservedQty: number;
  AvailableQty: number;
}

export default function StockSummary() {
  const [stock, setStock] = useState<Stock[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  useEffect(() => {
    (async () => {
      try {
        const res = await api.get<Stock[]>("/inventory/stock");
        setStock(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load stock"));
      } finally {
        setLoading(false);
      }
    })();
  }, []);
  return (
    <Layout>
      <div className="page-header"><h1>Stock Summary</h1></div>
      {error && <div className="alert alert-error">{error}</div>}
      {loading ? <div className="spinner" /> : (
        <div className="card table-wrap">
          <table className="table">
            <thead><tr><th>Warehouse</th><th>SKU</th><th>Product</th><th>On Hand</th><th>Reserved</th><th>Available</th></tr></thead>
            <tbody>{stock.map((s,i)=>(<tr key={i}><td>{s.WarehouseName}</td><td className="mono">{s.SKU}</td><td>{s.ProductName}</td><td>{s.QuantityOnHand}</td><td>{s.ReservedQty}</td><td>{s.AvailableQty}</td></tr>))}</tbody>
          </table>
        </div>
      )}
    </Layout>
  );
}
