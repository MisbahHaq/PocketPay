import React, { useState, useEffect, useCallback } from "react";
import { apiFetch } from "../api/client";
import { TransferSummary, TransfersPageResponse } from "../api/types";
import { Pagination } from "../components/Pagination";
import { Modal } from "../components/Modal";
import { formatPktDateTime } from "../lib/dates";
import { formatMoney } from "../lib/money";
import { useIdempotencyKey } from "../hooks/useIdempotencyKey";
import { messageForCode } from "../lib/errors";

interface TransfersProps {
  initialStatus?: string;
  initialPage?: number;
  onFilterChange?: (params: { status?: string; page?: number }) => void;
}

export function Transfers({
  initialStatus = "",
  initialPage = 1,
  onFilterChange,
}: TransfersProps) {
  const [transfers, setTransfers] = useState<TransferSummary[]>([]);
  const [statusFilter, setStatusFilter] = useState<string>(initialStatus);
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");
  const [page, setPage] = useState<number>(initialPage);
  const [pageSize] = useState<number>(10);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Reversal Modal State
  const [reversingTransfer, setReversingTransfer] = useState<TransferSummary | null>(null);
  const [reversalReason, setReversalReason] = useState<string>("");
  const [reversalLoading, setReversalLoading] = useState<boolean>(false);
  const [reversalError, setReversalError] = useState<string | null>(null);
  const [reversalSuccess, setReversalSuccess] = useState<boolean>(false);

  const { ensure: ensureIdempotencyKey, reset: resetIdempotencyKey } = useIdempotencyKey();

  const fetchTransfers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      let url = `/api/admin/transfers?Page=${page}&PageSize=${pageSize}`;
      if (statusFilter) {
        url += `&Status=${encodeURIComponent(statusFilter)}`;
      }
      if (fromDate) {
        url += `&From=${encodeURIComponent(new Date(fromDate).toISOString())}`;
      }
      if (toDate) {
        url += `&To=${encodeURIComponent(new Date(toDate).toISOString())}`;
      }

      const res = await apiFetch(url);
      if (!res.ok) {
        setError(`Failed to load transfers (Status: ${res.status})`);
        setLoading(false);
        return;
      }

      const data: TransfersPageResponse = await res.json();
      setTransfers(data.items ?? []);
      setTotalCount(data.totalCount ?? 0);
    } catch (err) {
      console.error("Error fetching transfers:", err);
      setError("Unable to connect to transfers service.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, statusFilter, fromDate, toDate]);

  useEffect(() => {
    fetchTransfers();
  }, [fetchTransfers]);

  const handleStatusChange = (newStatus: string) => {
    setStatusFilter(newStatus);
    setPage(1);
    onFilterChange?.({ status: newStatus || undefined, page: 1 });
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    onFilterChange?.({ status: statusFilter || undefined, page: newPage });
  };

  const openReversalModal = (t: TransferSummary) => {
    setReversingTransfer(t);
    setReversalReason(`Admin reversal of transfer ${t.id.slice(0, 8)}`);
    setReversalError(null);
    setReversalSuccess(false);
    ensureIdempotencyKey();
  };

  const closeReversalModal = () => {
    setReversingTransfer(null);
    setReversalError(null);
    setReversalSuccess(false);
    resetIdempotencyKey();
  };

  const handleReversalSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reversingTransfer) return;

    setReversalLoading(true);
    setReversalError(null);
    setReversalSuccess(false);

    const idemKey = ensureIdempotencyKey();

    try {
      const res = await apiFetch(`/api/admin/transfers/${reversingTransfer.id}/reverse`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": idemKey,
        },
        body: JSON.stringify({
          description: reversalReason,
        }),
      });

      if (!res.ok) {
        let errCode = "BUSINESS_RULE";
        try {
          const problem = await res.json();
          errCode = problem.code || problem.title || errCode;
        } catch {
          // ignore
        }
        setReversalError(messageForCode(errCode));
        setReversalLoading(false);
        return;
      }

      setReversalSuccess(true);
      resetIdempotencyKey();
      fetchTransfers();
    } catch (err) {
      console.error("Reversal error:", err);
      setReversalError("Failed to reverse transfer. Please check connection.");
    } finally {
      setReversalLoading(false);
    }
  };

  const renderStatusBadge = (status: TransferSummary["status"]) => {
    const s = String(status);
    let badgeClass = "badge-neutral";
    if (s === "Completed" || s === "1") badgeClass = "badge-success";
    else if (s === "Failed" || s === "2") badgeClass = "badge-danger";
    else if (s === "Reversed" || s === "3") badgeClass = "badge-purple";
    else if (s === "Pending" || s === "0") badgeClass = "badge-warning";

    return <span className={`badge ${badgeClass}`}>{s}</span>;
  };

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h1 className="page-title">Wallet Transfers</h1>
          <p className="page-description">
            Complete audit record of peer-to-peer and bill payments transfers with lifecycle state tracking.
          </p>
        </div>
        <div className="page-header-actions">
          <button
            onClick={fetchTransfers}
            disabled={loading}
            className="btn btn-secondary"
          >
            {loading ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* Filter and Date Bar */}
      <div className="table-filter-bar">
        <div className="filter-group">
          <label className="filter-label" htmlFor="status-filter">
            Status:
          </label>
          <select
            id="status-filter"
            className="form-select"
            value={statusFilter}
            onChange={(e) => handleStatusChange(e.target.value)}
          >
            <option value="">All Statuses</option>
            <option value="Completed">Completed</option>
            <option value="Pending">Pending</option>
            <option value="Failed">Failed</option>
            <option value="Reversed">Reversed</option>
          </select>
        </div>

        <div className="filter-group">
          <label className="filter-label" htmlFor="from-filter">
            From:
          </label>
          <input
            id="from-filter"
            type="date"
            className="form-input form-input-sm"
            value={fromDate}
            onChange={(e) => {
              setFromDate(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div className="filter-group">
          <label className="filter-label" htmlFor="to-filter">
            To:
          </label>
          <input
            id="to-filter"
            type="date"
            className="form-input form-input-sm"
            value={toDate}
            onChange={(e) => {
              setToDate(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <div className="filter-count text-muted text-sm ml-auto">
          {totalCount} total transfers recorded
        </div>
      </div>

      {/* Transfers Table */}
      {loading && transfers.length === 0 ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Loading transfers ledger...</p>
        </div>
      ) : transfers.length === 0 ? (
        <div className="empty-state">
          <p className="empty-state-title">No transfers found</p>
          <p className="empty-state-text">
            {statusFilter
              ? `No transfers with status "${statusFilter}" were found.`
              : "No transfer records present in the ledger."}
          </p>
        </div>
      ) : (
        <div className="table-wrapper">
          <table className="data-table">
            <thead>
              <tr>
                <th>Transfer ID</th>
                <th>Source Wallet</th>
                <th>Destination</th>
                <th>Amount</th>
                <th>Fee</th>
                <th>Status</th>
                <th>Created (PKT)</th>
                <th>Note / Reason</th>
                <th className="text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {transfers.map((t) => (
                <tr key={t.id}>
                  <td className="font-mono text-muted text-xs">
                    <span title={t.id}>{t.id.slice(0, 10)}...</span>
                  </td>
                  <td className="font-mono text-xs text-muted">
                    <span title={t.sourceWalletId}>{t.sourceWalletId.slice(0, 8)}...</span>
                  </td>
                  <td className="font-mono text-xs text-muted">
                    <span title={t.destinationWalletId}>{t.destinationWalletId.slice(0, 8)}...</span>
                  </td>
                  <td className="font-bold">
                    {formatMoney(t.amountMinorUnits, t.currency)}
                  </td>
                  <td className="text-sm text-muted">
                    {t.feeMinorUnits > 0
                      ? formatMoney(t.feeMinorUnits, t.currency)
                      : "0.00"}
                  </td>
                  <td>{renderStatusBadge(t.status)}</td>
                  <td className="text-sm">{formatPktDateTime(t.createdAt)}</td>
                  <td className="text-sm text-muted">
                    {t.failureReason ? (
                      <span className="text-danger" title={t.failureReason}>
                        {t.failureReason}
                      </span>
                    ) : (
                      t.note || "—"
                    )}
                  </td>
                  <td className="text-right">
                    {(t.status === "Completed" || t.status === 1) && (
                      <button
                        className="btn btn-outline-danger btn-xs"
                        onClick={() => openReversalModal(t)}
                        title="Reverse completed transfer (creates mirror journal entry)"
                      >
                        Reverse
                      </button>
                    )}
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

      {/* Reversal Confirmation Modal */}
      <Modal
        isOpen={reversingTransfer !== null}
        onClose={closeReversalModal}
        title="Administrative Transfer Reversal"
      >
        <form onSubmit={handleReversalSubmit} className="modal-form">
          {reversalError && <div className="alert alert-danger">{reversalError}</div>}
          {reversalSuccess && (
            <div className="alert alert-success">
              <strong>Transfer reversed successfully!</strong>
              <p className="text-sm mt-1">
                An equal-and-opposite mirror journal entry was created and the transfer status has been updated to Reversed.
              </p>
            </div>
          )}

          {reversingTransfer && (
            <div className="summary-box">
              <div className="summary-row">
                <span className="text-muted">Transfer Amount:</span>
                <span className="font-bold">
                  {formatMoney(
                    reversingTransfer.amountMinorUnits,
                    reversingTransfer.currency,
                  )}
                </span>
              </div>
              <div className="summary-row">
                <span className="text-muted">Transfer ID:</span>
                <span className="font-mono text-xs">{reversingTransfer.id}</span>
              </div>
            </div>
          )}

          <p className="modal-help-text">
            Per ADR-009, ledger entries are immutable. This action does not delete the transfer;
            it records a reversal journal entry that debits the destination and credits the source wallet.
          </p>

          <div className="form-group">
            <label className="form-label" htmlFor="reversal-reason">
              Reason for Reversal
            </label>
            <input
              id="reversal-reason"
              type="text"
              required
              className="form-input"
              value={reversalReason}
              onChange={(e) => setReversalReason(e.target.value)}
              placeholder="e.g. Disputed transaction resolution"
            />
          </div>

          <div className="modal-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={closeReversalModal}
            >
              {reversalSuccess ? "Close" : "Cancel"}
            </button>
            {!reversalSuccess && (
              <button
                type="submit"
                className="btn btn-danger"
                disabled={reversalLoading}
              >
                {reversalLoading ? "Reversing..." : "Confirm & Post Reversal"}
              </button>
            )}
          </div>
        </form>
      </Modal>
    </div>
  );
}
