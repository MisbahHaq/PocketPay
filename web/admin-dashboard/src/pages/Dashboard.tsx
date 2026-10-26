import { useState, useEffect, useCallback } from "react";
import { apiFetch } from "../api/client";
import { TrialBalanceResponse, TransferSummary } from "../api/types";
import { TrialBalancePanel } from "../components/TrialBalancePanel";
import { formatMoney } from "../lib/money";
import { formatPktDateTime } from "../lib/dates";

interface DashboardProps {
  onNavigate?: (path: string) => void;
}

export function Dashboard({ onNavigate }: DashboardProps) {
  const [trialBalance, setTrialBalance] = useState<TrialBalanceResponse | null>(null);
  const [transfers, setTransfers] = useState<TransferSummary[]>([]);
  const [usersCount, setUsersCount] = useState<number>(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [healthLive, setHealthLive] = useState<boolean | null>(null);
  const [healthReady, setHealthReady] = useState<boolean | null>(null);

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      // Fetch trial balance
      const tbRes = await apiFetch("/api/admin/ledger/trial-balance");
      if (tbRes.ok) {
        const tbData: TrialBalanceResponse = await tbRes.json();
        setTrialBalance(tbData);
      }

      // Fetch users count
      const usersRes = await apiFetch("/api/admin/users?Page=1&PageSize=1");
      if (usersRes.ok) {
        const usersData = await usersRes.json();
        setUsersCount(usersData.totalCount ?? 0);
      }

      // Fetch recent transfers
      const transfersRes = await apiFetch("/api/admin/transfers?Page=1&PageSize=5");
      if (transfersRes.ok) {
        const transfersData = await transfersRes.json();
        setTransfers(transfersData.items ?? []);
      }

      // Check health
      apiFetch("/health/live")
        .then((r) => setHealthLive(r.ok))
        .catch(() => setHealthLive(false));
      apiFetch("/health/ready")
        .then((r) => setHealthReady(r.ok))
        .catch(() => setHealthReady(false));
    } catch (err) {
      console.error("Dashboard fetch error:", err);
      setError("Unable to load latest dashboard metrics.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const pkrLine = trialBalance?.currencies.find((c) => c.currency === "PKR");
  const usdLine = trialBalance?.currencies.find((c) => c.currency === "USD");

  const completedTransfers = transfers.filter(
    (t) => t.status === "Completed" || t.status === 1,
  );
  const totalCompletedVolume = completedTransfers.reduce(
    (acc, t) => acc + BigInt(t.amountMinorUnits),
    0n,
  );

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h1 className="page-title">Executive Dashboard</h1>
          <p className="page-description">
            Real-time platform overview, double-entry ledger health, and transaction metrics.
          </p>
        </div>
        <div className="page-header-actions">
          <button
            onClick={fetchData}
            disabled={loading}
            className="btn btn-secondary"
          >
            {loading ? "Refreshing..." : "Refresh Metrics"}
          </button>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* KPI Stats Grid */}
      <div className="kpi-grid">
        <div className="kpi-card">
          <div className="kpi-label">Ledger Volume (PKR)</div>
          <div className="kpi-value">
            {pkrLine ? formatMoney(pkrLine.totalDebits, "PKR") : "0.00 PKR"}
          </div>
          <div className="kpi-subtext">Balanced ledger debits & credits</div>
        </div>

        <div className="kpi-card">
          <div className="kpi-label">Registered Users</div>
          <div className="kpi-value">{usersCount}</div>
          <div className="kpi-subtext">Active wallet accounts</div>
        </div>

        <div className="kpi-card">
          <div className="kpi-label">Recent Completed Volume</div>
          <div className="kpi-value">
            {formatMoney(totalCompletedVolume, "PKR")}
          </div>
          <div className="kpi-subtext">{completedTransfers.length} completed recently</div>
        </div>

        <div className="kpi-card">
          <div className="kpi-label">System Health</div>
          <div className="kpi-value status-badge-row">
            <span
              className={`badge ${healthLive ? "badge-success" : "badge-neutral"}`}
            >
              Live {healthLive ? "OK" : "—"}
            </span>
            <span
              className={`badge ${healthReady ? "badge-success" : "badge-neutral"}`}
            >
              Ready {healthReady ? "OK" : "—"}
            </span>
          </div>
          <div className="kpi-subtext">ASP.NET Core API health status</div>
        </div>
      </div>

      {/* Trial Balance Panel */}
      <section className="section-block">
        <TrialBalancePanel
          balanced={trialBalance?.isBalanced ?? true}
          difference={
            trialBalance && !trialBalance.isBalanced
              ? formatMoney(
                trialBalance.currencies.reduce((acc, c) => acc + Math.abs(c.difference), 0),
                "PKR",
              )
              : "0.00 PKR"
          }
          currencies={trialBalance?.currencies}
          onRefresh={fetchData}
          isLoading={loading}
        />
      </section>

      {/* Quick Action Buttons */}
      <section className="section-block">
        <h2 className="section-title">Administrative Actions</h2>
        <div className="quick-actions-row">
          <button
            onClick={() => onNavigate?.("users")}
            className="action-card"
          >
            <div className="action-card-icon">👥</div>
            <div className="action-card-text">
              <span className="action-card-title">Manage & Fund Users</span>
              <span className="action-card-desc">
                View registered users, inspect lockout state, and simulate wallet deposits.
              </span>
            </div>
          </button>

          <button
            onClick={() => onNavigate?.("transfers")}
            className="action-card"
          >
            <div className="action-card-icon">💸</div>
            <div className="action-card-text">
              <span className="action-card-title">Audit Transfers & Reversals</span>
              <span className="action-card-desc">
                Review wallet transfers, track failures, and execute administrative reversals.
              </span>
            </div>
          </button>

          <button
            onClick={() => onNavigate?.("reconciliation")}
            className="action-card"
          >
            <div className="action-card-icon">⚖️</div>
            <div className="action-card-text">
              <span className="action-card-title">Settlement Reconciliation</span>
              <span className="action-card-desc">
                Trigger daily settlement runs against billers and inspect unmatched items.
              </span>
            </div>
          </button>

          <button
            onClick={() => onNavigate?.("audit")}
            className="action-card"
          >
            <div className="action-card-icon">🔒</div>
            <div className="action-card-text">
              <span className="action-card-title">Tamper-Evident Audit Log</span>
              <span className="action-card-desc">
                Inspect append-only audit trail verified with cryptographic SHA-256 hash chaining.
              </span>
            </div>
          </button>
        </div>
      </section>

      {/* Recent Activity Feed */}
      <section className="section-block">
        <div className="section-header-row">
          <h2 className="section-title">Recent Money Movements</h2>
          <button
            onClick={() => onNavigate?.("transfers")}
            className="btn btn-ghost btn-sm"
          >
            View All &rarr;
          </button>
        </div>

        {transfers.length === 0 ? (
          <div className="empty-state">
            <p className="empty-state-text">No recent transfers recorded in the platform.</p>
          </div>
        ) : (
          <div className="table-wrapper">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Transfer ID</th>
                  <th>Amount</th>
                  <th>Fee</th>
                  <th>Status</th>
                  <th>Date (PKT)</th>
                  <th>Note</th>
                </tr>
              </thead>
              <tbody>
                {transfers.map((t) => (
                  <tr key={t.id}>
                    <td className="font-mono text-muted text-xs">
                      {t.id.slice(0, 8)}...
                    </td>
                    <td className="font-bold">
                      {formatMoney(t.amountMinorUnits, t.currency)}
                    </td>
                    <td className="text-muted">
                      {t.feeMinorUnits > 0
                        ? formatMoney(t.feeMinorUnits, t.currency)
                        : "None"}
                    </td>
                    <td>
                      <span
                        className={`badge badge-${t.status === "Completed" || t.status === 1
                          ? "success"
                          : t.status === "Failed" || t.status === 2
                            ? "danger"
                            : t.status === "Reversed" || t.status === 3
                              ? "purple"
                              : "warning"
                          }`}
                      >
                        {String(t.status)}
                      </span>
                    </td>
                    <td className="text-sm">
                      {formatPktDateTime(t.createdAt)}
                    </td>
                    <td className="text-sm text-muted">
                      {t.note || t.failureReason || "—"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
