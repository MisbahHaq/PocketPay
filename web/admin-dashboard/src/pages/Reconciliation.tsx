import React, { useState, useEffect, useCallback } from "react";
import { apiFetch } from "../api/client";
import { ReconciliationRun } from "../api/types";
import { formatPktDateTime, toPktDate } from "../lib/dates";

export function Reconciliation() {
  const [runs, setRuns] = useState<ReconciliationRun[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Trigger Run Form State
  const [billerCode, setBillerCode] = useState<string>("MOCK");
  const [businessDate, setBusinessDate] = useState<string>(() =>
    toPktDate(new Date().toISOString()),
  );
  const [triggerLoading, setTriggerLoading] = useState<boolean>(false);
  const [triggerSuccess, setTriggerSuccess] = useState<string | null>(null);
  const [triggerError, setTriggerError] = useState<string | null>(null);

  // Selected run for detail view
  const [selectedRun, setSelectedRun] = useState<ReconciliationRun | null>(null);

  const fetchRuns = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await apiFetch("/api/admin/reconciliation/runs");
      if (!res.ok) {
        setError(`Failed to fetch reconciliation runs (Status: ${res.status})`);
        setLoading(false);
        return;
      }
      const data = await res.json();
      setRuns(Array.isArray(data) ? data : []);
    } catch (err) {
      console.error("Reconciliation fetch error:", err);
      setError("Unable to connect to reconciliation service.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchRuns();
  }, [fetchRuns]);

  const handleTriggerRun = async (e: React.FormEvent) => {
    e.preventDefault();
    setTriggerLoading(true);
    setTriggerError(null);
    setTriggerSuccess(null);

    try {
      const res = await apiFetch("/api/admin/reconciliation/runs", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          billerCode,
          businessDate,
        }),
      });

      if (!res.ok) {
        setTriggerError(`Failed to trigger run (Status: ${res.status})`);
        setTriggerLoading(false);
        return;
      }

      const data = await res.json();
      setTriggerSuccess(`Run successfully queued! Run ID: ${data.runId || "Pending"}`);
      fetchRuns();
    } catch (err) {
      console.error("Trigger error:", err);
      setTriggerError("Failed to trigger reconciliation run.");
    } finally {
      setTriggerLoading(false);
    }
  };

  const totalMatched = runs.reduce((acc, r) => acc + (r.matchedCount || 0), 0);
  const totalMissing = runs.reduce((acc, r) => acc + (r.missingAtBillerCount || 0), 0);
  const totalUnknown = runs.reduce((acc, r) => acc + (r.unknownAtOurSideCount || 0), 0);
  const totalMismatch = runs.reduce((acc, r) => acc + (r.amountMismatchCount || 0), 0);

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h1 className="page-title">Settlement Reconciliation</h1>
          <p className="page-description">
            Automated and on-demand daily reconciliation between internal bill payments and external utility billers.
          </p>
        </div>
        <div className="page-header-actions">
          <button
            onClick={fetchRuns}
            disabled={loading}
            className="btn btn-secondary"
          >
            {loading ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* Reconciliation Invariant KPI Cards */}
      <div className="kpi-grid">
        <div className="kpi-card">
          <div className="kpi-label">Matched Payments</div>
          <div className="kpi-value text-success">{totalMatched}</div>
          <div className="kpi-subtext">Payments matched with biller settlement</div>
        </div>

        <div className="kpi-card">
          <div className="kpi-label">Missing at Biller</div>
          <div className={`kpi-value ${totalMissing > 0 ? "text-warning" : "text-muted"}`}>
            {totalMissing}
          </div>
          <div className="kpi-subtext">Requires review / biller inquiry</div>
        </div>

        <div className="kpi-card">
          <div className="kpi-label">Unknown at Our Side</div>
          <div className={`kpi-value ${totalUnknown > 0 ? "text-warning" : "text-muted"}`}>
            {totalUnknown}
          </div>
          <div className="kpi-subtext">Biller reported untracked payment</div>
        </div>

        <div className="kpi-card">
          <div className="kpi-label">Amount Mismatches</div>
          <div className={`kpi-value ${totalMismatch > 0 ? "text-danger" : "text-muted"}`}>
            {totalMismatch}
          </div>
          <div className="kpi-subtext">Paisa difference between records</div>
        </div>
      </div>

      {/* Trigger Run Form Card */}
      <section className="section-block">
        <div className="card">
          <h2 className="section-title">Trigger Manual Reconciliation Run</h2>
          <p className="card-subtitle mb-4">
            Imports the biller settlement file for the chosen business date, executes the 9-category
            matching algorithm, and reconciles the <code>SYSTEM_BILLER_SETTLEMENT</code> account.
          </p>

          {triggerError && <div className="alert alert-danger">{triggerError}</div>}
          {triggerSuccess && <div className="alert alert-success">{triggerSuccess}</div>}

          <form onSubmit={handleTriggerRun} className="form-inline-row">
            <div className="form-group">
              <label className="form-label" htmlFor="biller-code">
                Biller Code
              </label>
              <select
                id="biller-code"
                className="form-select"
                value={billerCode}
                onChange={(e) => setBillerCode(e.target.value)}
              >
                <option value="MOCK">MOCK (Mock Biller Service)</option>
                <option value="LESCO">LESCO (Lahore Electric)</option>
                <option value="SNGPL">SNGPL (Sui Northern Gas)</option>
                <option value="KE">KE (K-Electric)</option>
              </select>
            </div>

            <div className="form-group">
              <label className="form-label" htmlFor="business-date">
                Business Date (PKT)
              </label>
              <input
                id="business-date"
                type="date"
                className="form-input"
                required
                value={businessDate}
                onChange={(e) => setBusinessDate(e.target.value)}
              />
            </div>

            <div className="form-group form-action-align">
              <button
                type="submit"
                className="btn btn-primary"
                disabled={triggerLoading}
              >
                {triggerLoading ? "Executing Run..." : "Start Reconciliation Run"}
              </button>
            </div>
          </form>
        </div>
      </section>

      {/* Reconciliation Runs History Table */}
      <section className="section-block">
        <h2 className="section-title">Reconciliation Execution History</h2>

        {loading && runs.length === 0 ? (
          <div className="loading-state">
            <div className="spinner" />
            <p>Loading reconciliation history...</p>
          </div>
        ) : runs.length === 0 ? (
          <div className="empty-state">
            <p className="empty-state-title">No reconciliation runs recorded</p>
            <p className="empty-state-text">
              Run a manual reconciliation above to generate settlement results.
            </p>
          </div>
        ) : (
          <div className="table-wrapper">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Run ID</th>
                  <th>Biller</th>
                  <th>Business Date</th>
                  <th>Status</th>
                  <th>Started At (PKT)</th>
                  <th>Matched</th>
                  <th>Missing at Biller</th>
                  <th>Unknown at Ours</th>
                  <th>Mismatches</th>
                  <th className="text-right">Inspection</th>
                </tr>
              </thead>
              <tbody>
                {runs.map((r) => {
                  const s = String(r.status);
                  const isCompleted = s === "Completed" || s === "1";
                  const isRunning = s === "Running" || s === "0";
                  return (
                    <tr key={r.id}>
                      <td className="font-mono text-xs text-muted">
                        <span title={r.id}>{r.id.slice(0, 8)}...</span>
                      </td>
                      <td className="font-bold">{r.billerCode}</td>
                      <td>{r.businessDate}</td>
                      <td>
                        <span
                          className={`badge ${isCompleted
                            ? "badge-success"
                            : isRunning
                              ? "badge-warning"
                              : "badge-danger"
                            }`}
                        >
                          {s}
                        </span>
                      </td>
                      <td className="text-sm">{formatPktDateTime(r.startedAt)}</td>
                      <td className="font-bold text-success">{r.matchedCount ?? 0}</td>
                      <td className={r.missingAtBillerCount > 0 ? "text-warning font-bold" : "text-muted"}>
                        {r.missingAtBillerCount ?? 0}
                      </td>
                      <td className={r.unknownAtOurSideCount > 0 ? "text-warning font-bold" : "text-muted"}>
                        {r.unknownAtOurSideCount ?? 0}
                      </td>
                      <td className={r.amountMismatchCount > 0 ? "text-danger font-bold" : "text-muted"}>
                        {r.amountMismatchCount ?? 0}
                      </td>
                      <td className="text-right">
                        <button
                          className="btn btn-secondary btn-xs"
                          onClick={() => setSelectedRun(r)}
                        >
                          View Details
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Run Detail Modal */}
      {selectedRun && (
        <div className="modal-backdrop" onClick={() => setSelectedRun(null)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="modal-title">
                Reconciliation Run — {selectedRun.billerCode} ({selectedRun.businessDate})
              </h3>
              <button
                className="modal-close"
                onClick={() => setSelectedRun(null)}
              >
                &times;
              </button>
            </div>
            <div className="modal-body">
              <div className="summary-box">
                <div className="summary-row">
                  <span className="text-muted">Run ID:</span>
                  <span className="font-mono text-xs">{selectedRun.id}</span>
                </div>
                <div className="summary-row">
                  <span className="text-muted">Status:</span>
                  <span className="font-bold">{String(selectedRun.status)}</span>
                </div>
                <div className="summary-row">
                  <span className="text-muted">Execution Started:</span>
                  <span>{formatPktDateTime(selectedRun.startedAt)}</span>
                </div>
                {selectedRun.completedAt && (
                  <div className="summary-row">
                    <span className="text-muted">Completed At:</span>
                    <span>{formatPktDateTime(selectedRun.completedAt)}</span>
                  </div>
                )}
              </div>

              <h4 className="mt-4 font-bold text-sm">Matching Category Breakdown</h4>
              <div className="metrics-detail-grid mt-2">
                <div className="metric-box">
                  <div className="metric-box-title">Matched Payments</div>
                  <div className="metric-box-val text-success">{selectedRun.matchedCount}</div>
                </div>
                <div className="metric-box">
                  <div className="metric-box-title">Missing at Biller</div>
                  <div className="metric-box-val text-warning">{selectedRun.missingAtBillerCount}</div>
                </div>
                <div className="metric-box">
                  <div className="metric-box-title">Unknown at Our Side</div>
                  <div className="metric-box-val text-warning">{selectedRun.unknownAtOurSideCount}</div>
                </div>
                <div className="metric-box">
                  <div className="metric-box-title">Amount Mismatch</div>
                  <div className="metric-box-val text-danger">{selectedRun.amountMismatchCount}</div>
                </div>
              </div>

              <div className="modal-actions mt-4">
                <button
                  className="btn btn-secondary"
                  onClick={() => setSelectedRun(null)}
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
