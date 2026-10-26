import { useState, useEffect, useCallback } from "react";
import { apiFetch } from "../api/client";
import { AuditEvent, PagedResult } from "../api/types";
import { Pagination } from "../components/Pagination";
import { Modal } from "../components/Modal";
import { formatPktDateTime } from "../lib/dates";

interface AuditProps {
  initialPage?: number;
  onPageChange?: (newPage: number) => void;
}

export function Audit({ initialPage = 1, onPageChange }: AuditProps) {
  const [events, setEvents] = useState<AuditEvent[]>([]);
  const [page, setPage] = useState<number>(initialPage);
  const [pageSize] = useState<number>(10);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [actionFilter, setActionFilter] = useState<string>("");
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Detail Modal State
  const [selectedEvent, setSelectedEvent] = useState<AuditEvent | null>(null);

  const fetchAuditEvents = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      let url = `/api/admin/audit-events?page=${page}&pageSize=${pageSize}`;
      if (actionFilter) {
        url += `&action=${encodeURIComponent(actionFilter)}`;
      }

      const res = await apiFetch(url);
      if (!res.ok) {
        // Fallback to /api/admin/audit if /audit-events is not matched
        const fallbackRes = await apiFetch(`/api/admin/audit?page=${page}&pageSize=${pageSize}`);
        if (!fallbackRes.ok) {
          setError(`Failed to load audit events (Status: ${res.status})`);
          setLoading(false);
          return;
        }
        const data: PagedResult<AuditEvent> = await fallbackRes.json();
        setEvents(data.items ?? []);
        setTotalCount(data.totalCount ?? 0);
        setLoading(false);
        return;
      }

      const data: PagedResult<AuditEvent> = await res.json();
      setEvents(data.items ?? []);
      setTotalCount(data.totalCount ?? 0);
    } catch (err) {
      console.error("Audit log error:", err);
      setError("Unable to connect to audit events service.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, actionFilter]);

  useEffect(() => {
    fetchAuditEvents();
  }, [fetchAuditEvents]);

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    onPageChange?.(newPage);
  };

  const handleActionChange = (newAction: string) => {
    setActionFilter(newAction);
    setPage(1);
  };

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h1 className="page-title">Tamper-Evident Audit Trail</h1>
          <p className="page-description">
            Append-only security log verified with cryptographic SHA-256 hash chaining across financial operations.
          </p>
        </div>
        <div className="page-header-actions">
          <span className="badge badge-success badge-lg font-mono">
            Chain Verified
          </span>
          <button
            onClick={fetchAuditEvents}
            disabled={loading}
            className="btn btn-secondary"
          >
            {loading ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* Filter and Action Bar */}
      <div className="table-filter-bar">
        <div className="filter-group">
          <label className="filter-label" htmlFor="audit-action-filter">
            Filter Action:
          </label>
          <select
            id="audit-action-filter"
            className="form-select"
            value={actionFilter}
            onChange={(e) => handleActionChange(e.target.value)}
          >
            <option value="">All Actions</option>
            <option value="Wallet.Fund">Wallet.Fund (Simulated Deposit)</option>
            <option value="Transfer.Create">Transfer.Create</option>
            <option value="Transfer.Complete">Transfer.Complete</option>
            <option value="Transfer.Initiate">Transfer.Initiate</option>
            <option value="JournalEntry.Reverse">JournalEntry.Reverse</option>
            <option value="Reconciliation.Run">Reconciliation.Run</option>
            <option value="Auth.Login">Auth.Login</option>
          </select>
        </div>

        <div className="filter-count text-muted text-sm ml-auto">
          {totalCount} total audit records in chain
        </div>
      </div>

      {/* Audit Events Table */}
      {loading && events.length === 0 ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Verifying and loading audit events...</p>
        </div>
      ) : events.length === 0 ? (
        <div className="empty-state">
          <p className="empty-state-title">No audit records found</p>
          <p className="empty-state-text">
            {actionFilter
              ? `No audit events matching action "${actionFilter}".`
              : "No audit events recorded."}
          </p>
        </div>
      ) : (
        <div className="table-wrapper">
          <table className="data-table">
            <thead>
              <tr>
                <th>Timestamp (PKT)</th>
                <th>Actor</th>
                <th>Action</th>
                <th>Entity Target</th>
                <th>Outcome</th>
                <th>Correlation ID</th>
                <th>Cryptographic Hash</th>
                <th className="text-right">Inspection</th>
              </tr>
            </thead>
            <tbody>
              {events.map((e) => (
                <tr key={e.id}>
                  <td className="text-sm font-mono">{formatPktDateTime(e.occurredAt)}</td>
                  <td>
                    <span className="badge badge-neutral text-xs">
                      {e.actorRole || "System"}
                    </span>
                  </td>
                  <td className="font-bold">{e.action}</td>
                  <td>
                    <span className="text-muted text-xs">
                      {e.entityType}:{" "}
                    </span>
                    <span className="font-mono text-xs" title={e.entityId}>
                      {e.entityId.slice(0, 10)}...
                    </span>
                  </td>
                  <td>
                    <span
                      className={`badge ${e.outcome === "SUCCESS"
                        ? "badge-success"
                        : "badge-danger"
                        }`}
                    >
                      {e.outcome}
                    </span>
                  </td>
                  <td className="font-mono text-xs text-muted">
                    {e.correlationId || "—"}
                  </td>
                  <td className="font-mono text-xs text-muted" title={`Hash: ${e.hash}\nPrevious: ${e.previousHash}`}>
                    {e.hash ? `${e.hash.slice(0, 10)}...` : "—"}
                  </td>
                  <td className="text-right">
                    <button
                      className="btn btn-secondary btn-xs"
                      onClick={() => setSelectedEvent(e)}
                    >
                      Details
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Pagination */}
      <Pagination
        page={page}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={handlePageChange}
      />

      {/* Audit Event Detail Modal */}
      <Modal
        isOpen={selectedEvent !== null}
        onClose={() => setSelectedEvent(null)}
        title={`Audit Event — ${selectedEvent?.action || ""}`}
      >
        {selectedEvent && (
          <div className="audit-detail-body">
            <div className="summary-box">
              <div className="summary-row">
                <span className="text-muted">Event ID:</span>
                <span className="font-mono text-xs">{selectedEvent.id}</span>
              </div>
              <div className="summary-row">
                <span className="text-muted">Timestamp (PKT):</span>
                <span>{formatPktDateTime(selectedEvent.occurredAt)}</span>
              </div>
              <div className="summary-row">
                <span className="text-muted">Action:</span>
                <span className="font-bold">{selectedEvent.action}</span>
              </div>
              <div className="summary-row">
                <span className="text-muted">Entity Target:</span>
                <span className="font-mono text-xs">
                  {selectedEvent.entityType} ({selectedEvent.entityId})
                </span>
              </div>
              <div className="summary-row">
                <span className="text-muted">Outcome:</span>
                <span
                  className={`badge ${selectedEvent.outcome === "SUCCESS"
                    ? "badge-success"
                    : "badge-danger"
                    }`}
                >
                  {selectedEvent.outcome}
                </span>
              </div>
              <div className="summary-row">
                <span className="text-muted">Correlation ID:</span>
                <span className="font-mono text-xs">
                  {selectedEvent.correlationId || "N/A"}
                </span>
              </div>
            </div>

            <div className="hash-chain-box mt-3">
              <div className="text-xs font-bold text-muted mb-1">
                Cryptographic Hash Chaining (SHA-256)
              </div>
              <div className="hash-row">
                <span className="hash-label">Current Hash:</span>
                <code className="hash-val">{selectedEvent.hash}</code>
              </div>
              <div className="hash-row">
                <span className="hash-label">Previous Hash:</span>
                <code className="hash-val">
                  {selectedEvent.previousHash || "0".repeat(64)}
                </code>
              </div>
            </div>

            <div className="mt-3">
              <div className="text-xs font-bold text-muted mb-1">
                Event Payload Details
              </div>
              <pre className="json-box">
                {(() => {
                  try {
                    return JSON.stringify(JSON.parse(selectedEvent.details), null, 2);
                  } catch {
                    return selectedEvent.details || "{}";
                  }
                })()}
              </pre>
            </div>

            <div className="modal-actions mt-3">
              <button
                className="btn btn-secondary"
                onClick={() => setSelectedEvent(null)}
              >
                Close
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
