import { useCallback, useEffect, useState } from "react";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface Member {
  UserId: number;
  UserName: string;
  UserEmail: string;
  Status: string;
  Roles: string[];
  CreatedAt: string;
}

interface Invitation {
  InvitationId: number;
  Email: string;
  RoleName: string;
  InvitedBy: string;
  Status: string;
  CreatedAt: string;
  ExpiresAt: string;
}

interface RoleOption {
  RoleId: number;
  Name: string;
}

const STATUS_OPTIONS = ["Active", "Suspended", "Deactivated"];

const statusClass = (s: string) => {
  const map: Record<string, string> = {
    Active: "badge badge-mint",
    Suspended: "badge badge-coral",
    Deactivated: "badge badge-muted",
    Pending: "badge badge-yellow",
    Accepted: "badge badge-mint",
    Revoked: "badge badge-coral",
    Expired: "badge badge-muted",
  };
  return map[s] ?? "badge";
};

export default function Members() {
  const { user, has } = useAuth();
  const canManage = has("manage_users");
  const canInvite = has("invite_users");
  const canRemove = has("remove_users");

  const [tab, setTab] = useState<"members" | "invitations">("members");
  const [members, setMembers] = useState<Member[]>([]);
  const [invitations, setInvitations] = useState<Invitation[]>([]);
  const [roleOptions, setRoleOptions] = useState<RoleOption[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRoleId, setInviteRoleId] = useState(0);
  const [inviteBusy, setInviteBusy] = useState(false);
  const [newInviteUrl, setNewInviteUrl] = useState("");
  const [copiedId, setCopiedId] = useState<number | "new" | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      if (canManage) {
        const res = await api.get<Member[]>("/members");
        setMembers(res.data);
      }
      if (canInvite) {
        const [inv, roles] = await Promise.all([
          api.get<Invitation[]>("/invitations"),
          api.get<RoleOption[]>("/roles/assignable"),
        ]);
        setInvitations(inv.data);
        setRoleOptions(roles.data);
        if (roles.data.length && !inviteRoleId) setInviteRoleId(roles.data[0].RoleId);
      }
    } catch (err) {
      setError(getErrorMessage(err, "Failed to load members"));
    } finally {
      setLoading(false);
    }
  }, [canManage, canInvite]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    load();
  }, [load]);

  const flash = (msg: string) => {
    setError("");
    setSuccess(msg);
    setTimeout(() => setSuccess(""), 4000);
  };

  const fail = (err: unknown, fallback: string) => {
    setSuccess("");
    setError(getErrorMessage(err, fallback));
  };

  const copy = async (url: string, key: number | "new") => {
    try {
      await navigator.clipboard.writeText(url);
      setCopiedId(key);
      setTimeout(() => setCopiedId(null), 1800);
    } catch {
      setError("Clipboard unavailable — copy the link manually.");
    }
  };

  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    setInviteBusy(true);
    setNewInviteUrl("");
    try {
      const res = await api.post<{ AcceptUrl: string }>("/invitations", {
        Email: inviteEmail,
        RoleId: inviteRoleId,
      });
      setNewInviteUrl(res.data.AcceptUrl);
      setInviteEmail("");
      await load();
      flash("Invitation created. Copy the link and share it.");
    } catch (err) {
      fail(err, "Could not send the invitation");
    } finally {
      setInviteBusy(false);
    }
  };

  const copyInviteLink = async (id: number) => {
    try {
      const res = await api.get<{ acceptUrl: string }>(`/invitations/${id}/link`);
      await copy(res.data.acceptUrl, id);
    } catch (err) {
      fail(err, "Could not fetch the invitation link");
    }
  };

  const reissue = async (id: number) => {
    try {
      const res = await api.post<{ acceptUrl: string }>(`/invitations/${id}/reissue`);
      setNewInviteUrl(res.data.acceptUrl);
      await load();
      flash("A new link was generated — the old one no longer works.");
    } catch (err) {
      fail(err, "Could not reissue the invitation");
    }
  };

  const revoke = async (id: number) => {
    if (!window.confirm("Revoke this invitation?")) return;
    try {
      await api.delete(`/invitations/${id}`);
      await load();
      flash("Invitation revoked.");
    } catch (err) {
      fail(err, "Could not revoke the invitation");
    }
  };

  const changeRole = async (member: Member, roleId: number) => {
    try {
      await api.patch(`/members/${member.UserId}`, { RoleId: roleId });
      await load();
      flash(`${member.UserName}'s role updated.`);
    } catch (err) {
      fail(err, "Could not change the role");
      load();
    }
  };

  const changeStatus = async (member: Member, status: string) => {
    try {
      await api.patch(`/members/${member.UserId}`, { Status: status });
      await load();
      flash(`${member.UserName} is now ${status.toLowerCase()}.`);
    } catch (err) {
      fail(err, "Could not change the status");
      load();
    }
  };

  const removeMember = async (member: Member) => {
    if (!window.confirm(`Remove ${member.UserName} from the organization? Their history is kept.`))
      return;
    try {
      await api.delete(`/members/${member.UserId}`);
      await load();
      flash(`${member.UserName} was removed.`);
    } catch (err) {
      fail(err, "Could not remove the member");
    }
  };

  const currentRoleName = (m: Member) => m.Roles[0] ?? "";
  const selectedRole = (m: Member) =>
    roleOptions.find((r) => r.Name === currentRoleName(m))?.RoleId ?? 0;

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Members &amp; invitations</h1>
          <p className="subtitle">
            {members.length} {members.length === 1 ? "member" : "members"} &middot;{" "}
            {invitations.filter((i) => i.Status === "Pending").length} pending invitation(s)
          </p>
        </div>
        {canInvite && (
          <button className="btn btn-primary" onClick={() => setTab("invitations")}>
            + Invite someone
          </button>
        )}
      </div>

      {error && <div className="alert alert-error">{error}</div>}
      {success && <div className="alert alert-success">{success}</div>}

      <div className="tabs">
        <button
          className={`tab${tab === "members" ? " active" : ""}`}
          onClick={() => setTab("members")}
        >
          Members
        </button>
        {canInvite && (
          <button
            className={`tab${tab === "invitations" ? " active" : ""}`}
            onClick={() => setTab("invitations")}
          >
            Invitations
          </button>
        )}
      </div>

      {loading ? (
        <div className="spinner" />
      ) : tab === "members" ? (
        !canManage ? (
          <div className="state">
            <h3>Not allowed</h3>
            <p>You don't have permission to view the member list.</p>
          </div>
        ) : (
          <div className="card table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Member</th>
                  <th>Role</th>
                  <th>Status</th>
                  <th>Joined</th>
                  <th style={{ textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {members.map((m) => {
                  const isSelf = m.UserId === user?.userId;
                  return (
                    <tr key={m.UserId}>
                      <td>
                        <div className="cell-strong">{m.UserName}{isSelf && <span className="muted"> (you)</span>}</div>
                        <div className="cell-sub">{m.UserEmail}</div>
                      </td>
                      <td>
                        {canManage && !isSelf && roleOptions.length ? (
                          <select
                            className="select select-sm"
                            value={selectedRole(m)}
                            onChange={(e) => changeRole(m, Number(e.target.value))}
                          >
                            {roleOptions.map((r) => (
                              <option key={r.RoleId} value={r.RoleId}>
                                {r.Name}
                              </option>
                            ))}
                          </select>
                        ) : (
                          <span className="badge">{currentRoleName(m) || "—"}</span>
                        )}
                      </td>
                      <td>
                        {canManage && !isSelf ? (
                          <select
                            className="select select-sm"
                            value={m.Status}
                            onChange={(e) => changeStatus(m, e.target.value)}
                          >
                            {STATUS_OPTIONS.map((s) => (
                              <option key={s} value={s}>
                                {s}
                              </option>
                            ))}
                          </select>
                        ) : (
                          <span className={statusClass(m.Status)}>{m.Status}</span>
                        )}
                      </td>
                      <td className="mono">{new Date(m.CreatedAt).toLocaleDateString()}</td>
                      <td style={{ textAlign: "right" }}>
                        {canRemove && !isSelf ? (
                          <button className="btn btn-danger btn-sm" onClick={() => removeMember(m)}>
                            Remove
                          </button>
                        ) : (
                          <span className="muted">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )
      ) : (
        <>
          {canInvite && (
            <form className="card mb-16" onSubmit={handleInvite}>
              <div className="section-title">Invite someone</div>
              <div className="form-row">
                <div className="field">
                  <label htmlFor="inv-email">Email address</label>
                  <input
                    id="inv-email"
                    type="email"
                    className="input"
                    value={inviteEmail}
                    onChange={(e) => setInviteEmail(e.target.value)}
                    placeholder="teammate@example.com"
                    required
                  />
                </div>
                <div className="field">
                  <label htmlFor="inv-role">Role</label>
                  <select
                    id="inv-role"
                    className="select"
                    value={inviteRoleId}
                    onChange={(e) => setInviteRoleId(Number(e.target.value))}
                    required
                  >
                    {roleOptions.map((r) => (
                      <option key={r.RoleId} value={r.RoleId}>
                        {r.Name}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              <button type="submit" className="btn btn-primary" disabled={inviteBusy}>
                {inviteBusy ? "Creating..." : "Create invitation"}
              </button>
            </form>
          )}

          {newInviteUrl && (
            <div className="card mb-16 invite-link-card">
              <div className="section-title">Share this link</div>
              <div className="copy-row">
                <input className="input mono" readOnly value={newInviteUrl} />
                <button
                  className={`btn ${copiedId === "new" ? "btn-primary" : "btn-outline"}`}
                  onClick={() => copy(newInviteUrl, "new")}
                >
                  {copiedId === "new" ? "Copied!" : "Copy"}
                </button>
              </div>
              <p className="hint">The link expires in 7 days and can only be used once.</p>
            </div>
          )}

          <div className="card table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Invited by</th>
                  <th>Status</th>
                  <th>Expires</th>
                  <th style={{ textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {invitations.length === 0 && (
                  <tr>
                    <td colSpan={6} className="muted center">
                      No invitations yet.
                    </td>
                  </tr>
                )}
                {invitations.map((inv) => (
                  <tr key={inv.InvitationId}>
                    <td className="cell-strong">{inv.Email}</td>
                    <td>
                      <span className="badge">{inv.RoleName}</span>
                    </td>
                    <td>{inv.InvitedBy}</td>
                    <td>
                      <span className={statusClass(inv.Status)}>{inv.Status}</span>
                    </td>
                    <td className="mono">{new Date(inv.ExpiresAt).toLocaleDateString()}</td>
                    <td style={{ textAlign: "right" }}>
                      {inv.Status === "Pending" ? (
                        <span className="action-row">
                          <button
                            className={`btn btn-sm ${copiedId === inv.InvitationId ? "btn-primary" : "btn-outline"}`}
                            onClick={() => copyInviteLink(inv.InvitationId)}
                          >
                            {copiedId === inv.InvitationId ? "Copied!" : "Copy link"}
                          </button>
                          <button className="btn btn-sm btn-outline" onClick={() => reissue(inv.InvitationId)}>
                            Reissue
                          </button>
                          <button className="btn btn-sm btn-danger" onClick={() => revoke(inv.InvitationId)}>
                            Revoke
                          </button>
                        </span>
                      ) : (
                        <span className="muted">—</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </Layout>
  );
}
