import React, { useState, useEffect, useCallback } from "react";
import { apiFetch } from "../api/client";
import { UserSummary, PagedResult, FundWalletResponse } from "../api/types";
import { Pagination } from "../components/Pagination";
import { Modal } from "../components/Modal";
import { formatPktDateTime } from "../lib/dates";
import { formatMoney, parseMoneyInput } from "../lib/money";
import { useIdempotencyKey } from "../hooks/useIdempotencyKey";
import { messageForCode } from "../lib/errors";

interface UsersProps {
  initialPage?: number;
  onPageChange?: (newPage: number) => void;
}

export function Users({ initialPage = 1, onPageChange }: UsersProps) {
  const [users, setUsers] = useState<UserSummary[]>([]);
  const [page, setPage] = useState<number>(initialPage);
  const [pageSize] = useState<number>(10);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [search, setSearch] = useState<string>("");
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Funding Modal State
  const [fundingUser, setFundingUser] = useState<UserSummary | null>(null);
  const [fundWalletId, setFundWalletId] = useState<string>("");
  const [fundAmountInput, setFundAmountInput] = useState<string>("1000.00");
  const [fundCurrency, setFundCurrency] = useState<string>("PKR");
  const [fundingLoading, setFundingLoading] = useState<boolean>(false);
  const [fundingError, setFundingError] = useState<string | null>(null);
  const [fundingSuccess, setFundingSuccess] = useState<FundWalletResponse | null>(null);

  const { ensure: ensureIdempotencyKey, reset: resetIdempotencyKey } = useIdempotencyKey();

  const fetchUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await apiFetch(`/api/admin/users?Page=${page}&PageSize=${pageSize}`);
      if (!res.ok) {
        setError(`Failed to load users (Status: ${res.status})`);
        setLoading(false);
        return;
      }
      const data: PagedResult<UserSummary> = await res.json();
      setUsers(data.items ?? []);
      setTotalCount(data.totalCount ?? 0);
    } catch (err) {
      console.error("Error fetching users:", err);
      setError("Unable to connect to users service.");
    } finally {
      setLoading(false);
    }
  }, [page, pageSize]);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    onPageChange?.(newPage);
  };

  const openFundingModal = (user: UserSummary) => {
    setFundingUser(user);
    setFundWalletId(user.id); // default to user ID if wallet ID is 1-to-1 or lookup
    setFundAmountInput("1000.00");
    setFundCurrency("PKR");
    setFundingError(null);
    setFundingSuccess(null);
    ensureIdempotencyKey();
  };

  const closeFundingModal = () => {
    setFundingUser(null);
    setFundingError(null);
    setFundingSuccess(null);
    resetIdempotencyKey();
  };

  const handleFundSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFundingLoading(true);
    setFundingError(null);
    setFundingSuccess(null);

    let minorUnits: bigint;
    try {
      minorUnits = parseMoneyInput(fundAmountInput, fundCurrency);
      if (minorUnits <= 0n) {
        throw new Error("Amount must be greater than zero.");
      }
    } catch (err: any) {
      setFundingError(err.message || "Invalid money amount.");
      setFundingLoading(false);
      return;
    }

    const idemKey = ensureIdempotencyKey();

    try {
      const res = await apiFetch(`/api/admin/wallets/${fundWalletId}/fund`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Idempotency-Key": idemKey,
        },
        body: JSON.stringify({
          amountMinorUnits: Number(minorUnits),
          currency: fundCurrency,
        }),
      });

      if (!res.ok) {
        let errCode = "VALIDATION_FAILED";
        try {
          const problem = await res.json();
          errCode = problem.code || problem.title || errCode;
        } catch {
          // ignore
        }
        setFundingError(messageForCode(errCode));
        setFundingLoading(false);
        return;
      }

      const responseData: FundWalletResponse = await res.json();
      setFundingSuccess(responseData);
      resetIdempotencyKey();
      fetchUsers();
    } catch (err: any) {
      console.error("Funding error:", err);
      setFundingError("Failed to fund wallet. Check network connection.");
    } finally {
      setFundingLoading(false);
    }
  };

  // Filter users by search term
  const filteredUsers = users.filter((u) => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return (
      u.email.toLowerCase().includes(q) ||
      u.id.toLowerCase().includes(q) ||
      u.role.toLowerCase().includes(q)
    );
  });

  return (
    <div className="page-container">
      <div className="page-header">
        <div>
          <h1 className="page-title">User Accounts</h1>
          <p className="page-description">
            Directory of registered customer and administrative accounts with balance funding controls.
          </p>
        </div>
        <div className="page-header-actions">
          <button
            onClick={fetchUsers}
            disabled={loading}
            className="btn btn-secondary"
          >
            {loading ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </div>

      {error && <div className="alert alert-danger">{error}</div>}

      {/* Filter and Search Bar */}
      <div className="table-filter-bar">
        <div className="search-box">
          <input
            type="text"
            className="form-input search-input"
            placeholder="Search by email, user ID, or role..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <div className="filter-count text-muted text-sm">
          {filteredUsers.length} of {totalCount} users
        </div>
      </div>

      {/* Users Table */}
      {loading && users.length === 0 ? (
        <div className="loading-state">
          <div className="spinner" />
          <p>Loading user accounts...</p>
        </div>
      ) : filteredUsers.length === 0 ? (
        <div className="empty-state">
          <p className="empty-state-title">No users found</p>
          <p className="empty-state-text">
            {search ? `No accounts match "${search}".` : "No registered users in the database."}
          </p>
        </div>
      ) : (
        <div className="table-wrapper">
          <table className="data-table">
            <thead>
              <tr>
                <th>User ID</th>
                <th>Email Address</th>
                <th>Role</th>
                <th>Registered (PKT)</th>
                <th>Account Status</th>
                <th className="text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredUsers.map((u) => (
                <tr key={u.id}>
                  <td className="font-mono text-muted text-xs">
                    <span title={u.id}>{u.id.slice(0, 13)}...</span>
                  </td>
                  <td className="font-bold">{u.email}</td>
                  <td>
                    <span
                      className={`badge ${u.role === "Admin" ? "badge-info" : "badge-neutral"
                        }`}
                    >
                      {u.role}
                    </span>
                  </td>
                  <td className="text-sm">{formatPktDateTime(u.createdAtUtc)}</td>
                  <td>
                    {u.isLockedOut ? (
                      <span className="badge badge-danger">Locked Out</span>
                    ) : (
                      <span className="badge badge-success">Active</span>
                    )}
                  </td>
                  <td className="text-right">
                    <button
                      className="btn btn-primary btn-xs"
                      onClick={() => openFundingModal(u)}
                      title="Simulate deposit / fund wallet"
                    >
                      Fund Wallet
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

      {/* Fund Wallet Modal */}
      <Modal
        isOpen={fundingUser !== null}
        onClose={closeFundingModal}
        title={`Fund Wallet — ${fundingUser?.email || ""}`}
      >
        <form onSubmit={handleFundSubmit} className="modal-form">
          {fundingError && <div className="alert alert-danger">{fundingError}</div>}
          {fundingSuccess && (
            <div className="alert alert-success">
              <strong>Wallet funded successfully!</strong>
              <div className="text-xs font-mono mt-1">
                Amount: {formatMoney(fundingSuccess.amountMinorUnits, fundingSuccess.currency)}
                <br />
                Journal Entry ID: {fundingSuccess.journalEntryId}
              </div>
            </div>
          )}

          <p className="modal-help-text">
            Simulates a customer deposit. Credits the customer wallet liability account and
            debits the platform <code>SYSTEM_FUNDING</code> asset account in a balanced double-entry
            journal entry.
          </p>

          <div className="form-group">
            <label className="form-label" htmlFor="fund-wallet-id">
              Wallet / Target ID
            </label>
            <input
              id="fund-wallet-id"
              type="text"
              className="form-input font-mono"
              required
              value={fundWalletId}
              onChange={(e) => setFundWalletId(e.target.value)}
            />
          </div>

          <div className="form-row">
            <div className="form-group form-flex-2">
              <label className="form-label" htmlFor="fund-amount">
                Deposit Amount (Major Units)
              </label>
              <input
                id="fund-amount"
                type="text"
                className="form-input"
                required
                value={fundAmountInput}
                onChange={(e) => setFundAmountInput(e.target.value)}
                placeholder="e.g. 5000.00"
              />
              <span className="form-hint">
                Preview:{" "}
                <strong>
                  {(() => {
                    try {
                      return formatMoney(
                        parseMoneyInput(fundAmountInput || "0", fundCurrency),
                        fundCurrency,
                      );
                    } catch {
                      return "Invalid amount";
                    }
                  })()}
                </strong>
              </span>
            </div>

            <div className="form-group form-flex-1">
              <label className="form-label" htmlFor="fund-currency">
                Currency
              </label>
              <select
                id="fund-currency"
                className="form-select"
                value={fundCurrency}
                onChange={(e) => setFundCurrency(e.target.value)}
              >
                <option value="PKR">PKR (Pakistani Rupee)</option>
                <option value="USD">USD (US Dollar)</option>
              </select>
            </div>
          </div>

          <div className="modal-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={closeFundingModal}
            >
              {fundingSuccess ? "Done" : "Cancel"}
            </button>
            {!fundingSuccess && (
              <button
                type="submit"
                className="btn btn-primary"
                disabled={fundingLoading}
              >
                {fundingLoading ? "Posting Journal..." : "Deposit & Post Journal"}
              </button>
            )}
          </div>
        </form>
      </Modal>
    </div>
  );
}
