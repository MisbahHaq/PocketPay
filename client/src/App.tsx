import { Routes, Route, Navigate } from "react-router-dom";
import Login from "./pages/Login";
import Register from "./pages/Register";
import Dashboard from "./pages/Dashboard";
import Products from "./pages/Products";
import StockSummary from "./pages/StockSummary";
import StockMovements from "./pages/StockMovements";
import PurchaseOrders from "./pages/PurchaseOrders";
import SalesOrders from "./pages/SalesOrders";
import Masters from "./pages/Masters";
import CreateOrganization from "./pages/CreateOrganization";
import InviteAccept from "./pages/InviteAccept";
import Members from "./pages/Members";
import Roles from "./pages/Roles";
import Audit from "./pages/Audit";
import Profile from "./pages/Profile";
import { useAuth } from "./context/AuthContext";

const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const { token, loading } = useAuth();
  if (loading) return <div className="spinner" />;
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
};

//must be signed in and Active (not suspended / deactivated / removed)
const AccountGate = ({ children }: { children: React.ReactNode }) => {
  const { user, loading, logout } = useAuth();
  if (loading) return <div className="spinner" />;
  if (!user) return <Navigate to="/login" replace />;
  if (user.status !== "Active") {
    return (
      <div className="auth-shell">
        <div className="auth-card">
          <div className="auth-brand">
            <span className="brand-mark">&#9670;</span> Inventory
          </div>
          <h1>Account {user.status.toLowerCase()}</h1>
          <p className="subtitle">
            Your account is <strong>{user.status.toLowerCase()}</strong>, so you cannot sign in
            right now. Contact your organization administrator.
          </p>
          <button className="btn btn-outline btn-block" onClick={logout}>
            Sign out
          </button>
        </div>
      </div>
    );
  }
  return <>{children}</>;
};

//signed in and Active, but has no organization yet -> send to org setup
const RequireOrg = ({ children }: { children: React.ReactNode }) => {
  const { user } = useAuth();
  if (user && !user.organization) return <Navigate to="/org/new" replace />;
  return <>{children}</>;
};

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/invite/:token" element={<InviteAccept />} />
      <Route
        path="/org/new"
        element={
          <ProtectedRoute>
            <AccountGate>
              <CreateOrganization />
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/dashboard"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <Dashboard />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/products"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <Products />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/stock"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <StockSummary />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/stock/movements"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <StockMovements />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/purchase-orders"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <PurchaseOrders />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/sales-orders"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <SalesOrders />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/masters"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <Masters />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/members"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <Members />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/roles"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <Roles />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/audit"
        element={
          <ProtectedRoute>
            <AccountGate>
              <RequireOrg>
                <Audit />
              </RequireOrg>
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route
        path="/profile"
        element={
          <ProtectedRoute>
            <AccountGate>
              <Profile />
            </AccountGate>
          </ProtectedRoute>
        }
      />
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}

export default App;
