import { Fragment, useCallback, useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface AuditItem {
  AuditLogId: number;
  Action: string;
  ActorName: string | null;
  TargetType: string | null;
  TargetId: string | null;
  Details: string | null;
  CreatedAt: string;
}

interface AuditPage {
  total: number;
  page: number;
  pageSize: number;
  items: AuditItem[];
}

const pretty = (details: string | null) => {
  if (!details) return "";
  try {
    return JSON.stringify(JSON.parse(details), null, 1);
  } catch {
    return details;
  }
};

export default function Audit() {
  const { has } = useAuth();
  const allowed = has("manage_organization");

  const [data, setData] = useState<AuditPage | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filter, setFilter] = useState("");
  const [openId, setOpenId] = useState<number | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const res = await api.get<AuditPage>("/auditlogs", {
        params: { page, pageSize: 50 },
      });
      setData(res.data);
    } catch (err) {
      setError(getErrorMessage(err, "Failed to load the audit log"));
    } finally {
      setLoading(false);
    }
  }, [page]);

  useEffect(() => {
    if (allowed) load();
    else setLoading(false);
  }, [allowed, load]);

  if (!allowed) {
    return (
      <Layout>
        <div className="state">
          <h3>Not allowed</h3>
          <p>You don't have permission to view the audit log.</p>
        </div>
      </Layout>
    );
  }

  const items = (data?.items ?? []).filter(
    (i) =>
      !filter.trim() ||
      i.Action.toLowerCase().includes(filter.toLowerCase()) ||
      (i.ActorName ?? "").toLowerCase().includes(filter.toLowerCase())
  );
  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Audit log</h1>
          <p className="subtitle">
            {data ? `${data.total} recorded events in this organization.` : "Every action, in order."}
          </p>
        </div>
        <input
          className="input filter-input"
          placeholder="Filter by action or actor..."
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
        />
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      {loading ? (
        <div className="spinner" />
      ) : (
        <div className="card table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>When</th>
                <th>Action</th>
                <th>Actor</th>
                <th>Target</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {items.length === 0 && (
                <tr>
                  <td colSpan={5} className="muted center">
                    Nothing matches.
                  </td>
                </tr>
              )}
              {items.map((i) => (
                <Fragment key={i.AuditLogId}>
                  <tr>
                    <td className="mono cell-nowrap">
                      {new Date(i.CreatedAt).toLocaleString()}
                    </td>
                    <td>
                      <span className="badge badge-sky mono">{i.Action}</span>
                    </td>
                    <td>{i.ActorName ?? <span className="muted">system</span>}</td>
                    <td>
                      {i.TargetType}
                      {i.TargetId && <span className="muted"> #{i.TargetId}</span>}
                    </td>
                    <td style={{ textAlign: "right" }}>
                      {i.Details && (
                        <button
                          className="btn btn-sm btn-ghost"
                          onClick={() => setOpenId(openId === i.AuditLogId ? null : i.AuditLogId)}
                        >
                          {openId === i.AuditLogId ? "Hide" : "Details"}
                        </button>
                      )}
                    </td>
                  </tr>
                  {openId === i.AuditLogId && (
                    <tr>
                      <td colSpan={5}>
                        <pre className="detail-json">{pretty(i.Details)}</pre>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>

          <div className="pagination">
            <button
              className="btn btn-sm btn-outline"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
            >
              &larr; Newer
            </button>
            <span className="muted">
              Page {page} of {totalPages}
            </span>
            <button
              className="btn btn-sm btn-outline"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Older &rarr;
            </button>
          </div>
        </div>
      )}
    </Layout>
  );
}
