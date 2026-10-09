import { useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface Product {
  ProductId: number;
  SKU: string;
  Name: string;
  CategoryName: string;
  UnitName: string;
  CostPrice: number;
  SellingPrice: number;
  MinReorderLevel: number;
  IsActive: boolean;
}

export default function Products() {
  const { has } = useAuth();
  const canManage = has("manage_products");
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      try {
        const res = await api.get<Product[]>("/products");
        setProducts(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load products"));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Products</h1>
          <p className="subtitle">{products.length} products</p>
        </div>
        {canManage && <button className="btn btn-primary">+ New product</button>}
      </div>
      {error && <div className="alert alert-error">{error}</div>}
      {loading ? <div className="spinner" /> : (
        <div className="card table-wrap">
          <table className="table">
            <thead>
              <tr><th>SKU</th><th>Name</th><th>Category</th><th>UOM</th><th>Cost</th><th>Sale</th><th>Reorder</th><th>Status</th></tr>
            </thead>
            <tbody>
              {products.map(p => (
                <tr key={p.ProductId}>
                  <td className="mono">{p.SKU}</td>
                  <td>{p.Name}</td>
                  <td>{p.CategoryName}</td>
                  <td>{p.UnitName}</td>
                  <td>{p.CostPrice}</td>
                  <td>{p.SellingPrice}</td>
                  <td>{p.MinReorderLevel}</td>
                  <td>{p.IsActive ? "Active" : "Inactive"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Layout>
  );
}
