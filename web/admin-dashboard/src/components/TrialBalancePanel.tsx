import { TrialBalanceCurrencyLine } from "../api/types";
import { formatMoney } from "../lib/money";

interface TrialBalancePanelProps {
  balanced: boolean;
  difference?: string;
  currencies?: TrialBalanceCurrencyLine[];
  onRefresh?: () => void;
  isLoading?: boolean;
}

export function TrialBalancePanel({
  balanced,
  difference = "0",
  currencies,
  onRefresh,
  isLoading,
}: TrialBalancePanelProps) {
  return (
    <div className={`trial-balance-card ${balanced ? "card-balanced" : "card-mismatch"}`}>
      <div className="card-header-row">
        <div className="card-title-group">
          <div className={`status-dot ${balanced ? "dot-balanced" : "dot-mismatch"}`} />
          <h3 className="card-title">Ledger Integrity & Trial Balance</h3>
        </div>
        <div className="card-actions-group">
          <span className={`status-pill ${balanced ? "pill-balanced" : "pill-mismatch"}`}>
            {balanced ? "BALANCED" : `MISMATCH ${difference}`}
          </span>
          {onRefresh && (
            <button
              onClick={onRefresh}
              disabled={isLoading}
              className="btn btn-secondary btn-sm"
              title="Refresh trial balance"
            >
              {isLoading ? "Checking..." : "Verify Ledger"}
            </button>
          )}
        </div>
      </div>

      <p className="card-subtitle">
        {balanced
          ? "Double-entry invariant holds: Total Debits equal Total Credits across all system and customer accounts."
          : `Integrity alert: Ledger difference of ${difference} detected across accounts.`}
      </p>

      {currencies && currencies.length > 0 && (
        <div className="currencies-grid">
          {currencies.map((line) => {
            const isZeroDiff = line.difference === 0;
            return (
              <div key={line.currency} className="currency-stat-box">
                <div className="currency-label">{line.currency} Ledger</div>
                <div className="currency-numbers">
                  <div className="currency-num-row">
                    <span className="text-muted">Total Debits:</span>
                    <span className="font-mono">{formatMoney(line.totalDebits, line.currency)}</span>
                  </div>
                  <div className="currency-num-row">
                    <span className="text-muted">Total Credits:</span>
                    <span className="font-mono">{formatMoney(line.totalCredits, line.currency)}</span>
                  </div>
                  <div className="currency-num-row difference-row">
                    <span className="text-muted">Net Difference:</span>
                    <span className={`font-mono font-bold ${isZeroDiff ? "text-success" : "text-danger"}`}>
                      {formatMoney(line.difference, line.currency)}
                    </span>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
