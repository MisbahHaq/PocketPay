import { useState, useEffect } from "react";
import { useHashRouter } from "./hooks/useHashRouter";
import { Navbar } from "./components/Navbar";
import { LoginModal } from "./components/LoginModal";
import { Dashboard } from "./pages/Dashboard";
import { Users } from "./pages/Users";
import { Transfers } from "./pages/Transfers";
import { Reconciliation } from "./pages/Reconciliation";
import { Audit } from "./pages/Audit";
import { tokenStore } from "./auth/tokenStore";
import { refreshOnce } from "./auth/refresh";
import { apiFetch } from "./api/client";

export default function App() {
  const { route, navigate, updateParams } = useHashRouter();
  const [isLoginOpen, setIsLoginOpen] = useState(false);
  const [sessionToast, setSessionToast] = useState<string | null>(null);
  const [, setAuthTick] = useState(0);

  // Subscribe to tokenStore updates
  useEffect(() => {
    return tokenStore.subscribe(() => {
      setAuthTick((t) => t + 1);
    });
  }, []);

  const handleRefreshSession = async () => {
    try {
      const newToken = await refreshOnce(async () => {
        const refresh = tokenStore.getRefresh();
        if (!refresh) {
          throw new Error("No refresh token available");
        }
        const res = await apiFetch("/api/auth/refresh", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken: refresh }),
        });
        if (!res.ok) {
          throw new Error("Failed to refresh session");
        }
        const data = await res.json();
        tokenStore.set(data.accessToken, data.refreshToken);
        return data.accessToken;
      });

      if (newToken) {
        showToast("Session token rotated and refreshed successfully!");
      }
    } catch {
      showToast("Session refresh failed. Please sign in again.");
    }
  };

  const showToast = (msg: string) => {
    setSessionToast(msg);
    setTimeout(() => {
      setSessionToast(null);
    }, 4000);
  };

  const currentPath = route.path;

  return (
    <div className="app-layout">
      <Navbar
        currentPath={currentPath}
        onNavigate={navigate}
        onOpenLogin={() => setIsLoginOpen(true)}
        onRefreshSession={handleRefreshSession}
      />

      {sessionToast && (
        <div className="toast-notification">
          <span>{sessionToast}</span>
          <button
            onClick={() => setSessionToast(null)}
            className="toast-close"
          >
            &times;
          </button>
        </div>
      )}

      <main className="main-content" role="main">
        {currentPath === "users" && (
          <Users
            initialPage={Number(route.params.page) || 1}
            onPageChange={(page) => updateParams({ page })}
          />
        )}
        {currentPath === "transfers" && (
          <Transfers
            initialStatus={route.params.status || ""}
            initialPage={Number(route.params.page) || 1}
            onFilterChange={({ status, page }) =>
              updateParams({ status, page })
            }
          />
        )}
        {currentPath === "reconciliation" && <Reconciliation />}
        {currentPath === "audit" && (
          <Audit
            initialPage={Number(route.params.page) || 1}
            onPageChange={(page) => updateParams({ page })}
          />
        )}
        {(currentPath === "" ||
          currentPath === "/" ||
          currentPath === "dashboard") && (
            <Dashboard onNavigate={navigate} />
          )}
      </main>

      <footer className="app-footer">
        <div className="footer-content">
          <span>PayPocket Admin Portal &copy; 2026</span>
          <span className="footer-separator">&bull;</span>
          <span>Asia/Karachi (PKT UTC+5)</span>
          <span className="footer-separator">&bull;</span>
          <span>Double-Entry Ledger v1.0</span>
        </div>
      </footer>

      <LoginModal
        isOpen={isLoginOpen}
        onClose={() => setIsLoginOpen(false)}
        onSuccess={() => showToast("Successfully signed in as Administrator.")}
      />
    </div>
  );
}
