import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import api from "../services/api";
import { useAuth, type AuthResponse } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface Lookup {
  Email: string;
  OrganizationName: string;
  RoleName: string;
  Status: string;
  ExpiresAt: string;
  HasAccount: boolean;
}

export default function InviteAccept() {
  const { token } = useParams<{ token: string }>();
  const { user, token: sessionToken, adoptSession, logout, loading } = useAuth();
  const navigate = useNavigate();

  const [lookup, setLookup] = useState<Lookup | null>(null);
  const [loadError, setLoadError] = useState("");
  const [checking, setChecking] = useState(true);
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const run = async () => {
      try {
        const res = await api.get<Lookup>("/invitations/lookup", { params: { token } });
        setLookup(res.data);
      } catch (err) {
        setLoadError(getErrorMessage(err, "Invitation not found"));
      } finally {
        setChecking(false);
      }
    };
    run();
  }, [token]);

  const accept = async (body: Record<string, unknown> = {}) => {
    setError("");
    setBusy(true);
    try {
      const res = await api.post<AuthResponse>("/invitations/accept", {
        Token: token,
        ...body,
      });
      await adoptSession(res.data.Token);
      sessionStorage.removeItem("pendingInvite");
      navigate("/dashboard", { replace: true });
    } catch (err) {
      setError(getErrorMessage(err, "Could not accept the invitation"));
    } finally {
      setBusy(false);
    }
  };

  const signInToAccept = () => {
    sessionStorage.setItem("pendingInvite", token ?? "");
    navigate("/login");
  };

  const switchAccount = () => {
    sessionStorage.setItem("pendingInvite", token ?? "");
    logout();
    navigate("/login");
  };

  if (loading || checking) return <div className="spinner" />;

  if (loadError || !lookup) {
    return (
      <div className="auth-shell">
        <div className="auth-card">
          <div className="auth-brand">
            <span className="brand-mark">&#9670;</span> Inventory
          </div>
          <h1>Invitation unavailable</h1>
          <p className="subtitle">{loadError || "This invitation link is not valid."}</p>
          <Link to="/login" className="btn btn-outline btn-block">
            Go to sign in
          </Link>
        </div>
      </div>
    );
  }

  const header = (
    <>
      <div className="auth-brand">
        <span className="brand-mark">&#9670;</span> Inventory
      </div>
      <h1>You're invited to {lookup.OrganizationName}</h1>
      <p className="subtitle">
        <span className="badge badge-yellow">{lookup.RoleName}</span>{" "}
        &nbsp;Invitation for <strong>{lookup.Email}</strong>
      </p>
    </>
  );

  const expiry = new Date(lookup.ExpiresAt).toLocaleDateString();

  if (lookup.Status !== "Pending") {
    const messages: Record<string, string> = {
      Accepted: "This invitation has already been used.",
      Revoked: "This invitation was revoked by an administrator.",
      Expired: "This invitation has expired. Ask for a new one.",
    };
    return (
      <div className="auth-shell">
        <div className="auth-card">
          {header}
          <div className="alert alert-error">{messages[lookup.Status] ?? "This invitation is no longer usable."}</div>
          <Link to="/login" className="btn btn-outline btn-block">
            Go to sign in
          </Link>
        </div>
      </div>
    );
  }

  //brand new person: create the account and join in one step
  if (!lookup.HasAccount) {
    return (
      <div className="auth-shell">
        <div className="auth-card">
          {header}
          <p className="subtitle">Choose a username and password to finish joining.</p>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              accept({ UserName: username, Password: password });
            }}
          >
            <div className="field">
              <label htmlFor="iv-username">Username</label>
              <input
                id="iv-username"
                className="input"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="e.g. jane"
                autoFocus
                required
              />
            </div>
            <div className="field">
              <label htmlFor="iv-password">Password</label>
              <input
                id="iv-password"
                type="password"
                className="input"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="At least 6 characters"
                minLength={6}
                required
              />
            </div>
            {error && <div className="alert alert-error">{error}</div>}
            <button type="submit" className="btn btn-primary btn-block" disabled={busy}>
              {busy ? "Joining..." : "Create account & join"}
            </button>
          </form>
          <p className="auth-footer">
            Already have an account? <button className="linklike" onClick={signInToAccept}>Sign in to accept</button>
          </p>
        </div>
      </div>
    );
  }

  //existing account
  const signedInAsRightUser = !!sessionToken && user?.userEmail?.toLowerCase() === lookup.Email.toLowerCase();

  if (!sessionToken) {
    return (
      <div className="auth-shell">
        <div className="auth-card">
          {header}
          <p className="subtitle">
            An account already exists for <strong>{lookup.Email}</strong>. Sign in to accept this
            invitation.
          </p>
          {error && <div className="alert alert-error">{error}</div>}
          <button className="btn btn-primary btn-block" onClick={signInToAccept}>
            Sign in to accept
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="auth-shell">
      <div className="auth-card">
        {header}
        <p className="subtitle">
          {signedInAsRightUser
            ? `You're signed in as ${user?.userName}. Accept to join as ${lookup.RoleName}.`
            : `You're signed in as ${user?.userName}. This invitation belongs to ${lookup.Email} — switch accounts to accept it.`}
        </p>
        <p className="hint">Expires on {expiry}.</p>
        {error && <div className="alert alert-error">{error}</div>}
        {signedInAsRightUser ? (
          <button className="btn btn-primary btn-block" onClick={() => accept()} disabled={busy}>
            {busy ? "Joining..." : "Accept invitation"}
          </button>
        ) : (
          <button className="btn btn-outline btn-block" onClick={switchAccount}>
            Sign out &amp; switch account
          </button>
        )}
      </div>
    </div>
  );
}
