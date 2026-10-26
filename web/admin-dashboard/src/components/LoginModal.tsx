import React, { useState } from "react";
import { Modal } from "./Modal";
import { apiFetch, setDemoMode } from "../api/client";
import { tokenStore } from "../auth/tokenStore";
import { messageForCode } from "../lib/errors";

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function LoginModal({ isOpen, onClose, onSuccess }: LoginModalProps) {
  const [email, setEmail] = useState("admin@paypocket.local");
  const [password, setPassword] = useState("Admin123!");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const response = await apiFetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!response.ok) {
        let errCode = "UNAUTHORIZED";
        try {
          const problem = await response.json();
          errCode = problem.code || problem.title || errCode;
        } catch {
          // ignore
        }
        setError(messageForCode(errCode));
        setLoading(false);
        return;
      }

      const data = await response.json();
      tokenStore.set(data.accessToken, data.refreshToken);
      setLoading(false);
      onSuccess();
      onClose();
    } catch {
      // Offline fallback: log in as demo admin
      setError("Server unreachable. Switched to offline demo mode.");
      setDemoMode(true);
      const demoToken = btoa(
        JSON.stringify({ alg: "HS256" }) +
        "." +
        btoa(
          JSON.stringify({
            sub: "a1000000-0000-0000-0000-000000000001",
            email,
            role: "Admin",
            exp: Math.floor(Date.now() / 1000) + 7200,
          }),
        ) +
        ".demo-signature",
      );
      tokenStore.set(demoToken, "demo-refresh-token");
      setLoading(false);
      onSuccess();
      onClose();
    }
  };

  const fillDemo = () => {
    setEmail("admin@paypocket.local");
    setPassword("Admin123!");
  };

  const enterDemoDirectly = () => {
    setDemoMode(true);
    const demoToken =
      btoa(JSON.stringify({ alg: "HS256" })) +
      "." +
      btoa(
        JSON.stringify({
          sub: "a1000000-0000-0000-0000-000000000001",
          email: "admin@paypocket.local",
          role: "Admin",
          exp: Math.floor(Date.now() / 1000) + 7200,
        }),
      ) +
      ".demo-signature";
    tokenStore.set(demoToken, "demo-refresh-token");
    onSuccess();
    onClose();
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Admin Authentication">
      <form onSubmit={handleSubmit} className="modal-form">
        {error && <div className="alert alert-danger">{error}</div>}

        <div className="form-group">
          <label className="form-label" htmlFor="admin-email">
            Admin Email Address
          </label>
          <input
            id="admin-email"
            type="email"
            required
            className="form-input"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="admin@paypocket.local"
          />
        </div>

        <div className="form-group">
          <label className="form-label" htmlFor="admin-password">
            Password
          </label>
          <input
            id="admin-password"
            type="password"
            required
            className="form-input"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="••••••••"
          />
        </div>

        <div className="form-help-row">
          <button
            type="button"
            className="btn-link"
            onClick={fillDemo}
          >
            Use Default Admin Credentials
          </button>
        </div>

        <div className="modal-actions">
          <button
            type="button"
            className="btn btn-secondary"
            onClick={enterDemoDirectly}
            title="Explore with pre-seeded mock data"
          >
            Continue in Demo Mode
          </button>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading}
          >
            {loading ? "Signing in..." : "Sign in to Admin"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
