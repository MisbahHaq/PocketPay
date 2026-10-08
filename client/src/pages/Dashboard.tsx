import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  BarChart,
  Bar,
  AreaChart,
  Area,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import api from "../services/api";
import Layout from "../components/Layout";
import { useAuth } from "../context/AuthContext";
import { getErrorMessage } from "../utils/errors";

interface Project {
  ProjectId: number;
  ProjectName: string;
  Description: string;
  CreatorName: string;
  CreatedAt: string;
}

const PALETTE = ["#fde047", "#bae6fd", "#a7f3d0", "#e9d5ff", "#fecdd3"];

type TooltipPayload = {
  name?: string;
  value?: number | string;
  color?: string;
  payload?: { name?: string; CreatorName?: string };
};

const BrutalTooltip = ({
  active,
  payload,
  label,
}: {
  active?: boolean;
  payload?: TooltipPayload[];
  label?: string | number;
}) => {
  if (!active || !payload?.length) return null;
  const title = label ?? payload[0]?.payload?.name ?? payload[0]?.name ?? "";

  return (
    <div className="chart-tooltip">
      {title !== "" && <div className="chart-tooltip-label">{title}</div>}
      {payload.map((entry, i) => (
        <div className="chart-tooltip-row" key={i}>
          <span>
            <span className="swatch" style={{ background: entry.color ?? PALETTE[i % PALETTE.length] }} />
            {entry.payload?.CreatorName ?? entry.name}
          </span>
          <strong>{entry.value}</strong>
        </div>
      ))}
    </div>
  );
};

export default function Dashboard() {
  const { user } = useAuth();
  const [projects, setProjects] = useState<Project[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    const load = async () => {
      try {
        const res = await api.get<Project[]>("/projects/all");
        setProjects(res.data);
      } catch (err) {
        setError(getErrorMessage(err, "Failed to load projects"));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, []);

  const byCreator = useMemo(() => {
    const map = new Map<string, number>();
    projects.forEach((p) => map.set(p.CreatorName, (map.get(p.CreatorName) ?? 0) + 1));
    return Array.from(map, ([name, count]) => ({ name, count }));
  }, [projects]);

  const overTime = useMemo(() => {
    const map = new Map<string, number>();
    projects.forEach((p) => {
      const d = new Date(p.CreatedAt);
      if (Number.isNaN(d.getTime())) return;
      const key = d.toISOString().slice(0, 10);
      map.set(key, (map.get(key) ?? 0) + 1);
    });
    const days = Array.from(map, ([date, count]) => ({ date, count })).sort((a, b) =>
      a.date.localeCompare(b.date)
    );
    let total = 0;
    return days.map((d) => ({ ...d, total: (total += d.count) }));
  }, [projects]);

  const roles = user?.roles ?? [];
  const canCreate = roles.includes("Manager") || roles.includes("Admin");

  return (
    <Layout>
      <div className="page-header">
        <div>
          <h1>Dashboard</h1>
          <p className="subtitle">Welcome back, {user?.userName}. Here's the workspace at a glance.</p>
        </div>
        <Link to="/projects" className="btn btn-primary">
          {canCreate ? "+ New project" : "View projects"}
        </Link>
      </div>

      {error && <div className="alert alert-error">{error}</div>}

      <div className="section-title">Overview</div>
      <div className="grid grid-3">
        <div className="card stat-card">
          <div className="stat-label">Projects</div>
          <div className="stat-value">{loading ? "--" : projects.length}</div>
          <div className="stat-hint">Total in workspace</div>
        </div>

        <div className="card stat-card">
          <div className="stat-label">Your role</div>
          <div className="stat-value">
            {roles.length ? (
              roles.map((r, i) => (
                <span className={`badge badge-${["yellow", "sky", "mint", "coral"][i % 4]}`} key={r} style={{ marginRight: 6 }}>
                  {r}
                </span>
              ))
            ) : (
              <span className="muted">None</span>
            )}
          </div>
          <div className="stat-hint">{canCreate ? "Can create projects" : "View access only"}</div>
        </div>

        <div className="card stat-card">
          <div className="stat-label">Account</div>
          <div className="stat-value">{user?.userName}</div>
          <div className="stat-hint">{user?.userEmail}</div>
        </div>
      </div>

      <div className="section-title mt-24">Analytics</div>
      {loading ? (
        <div className="spinner" />
      ) : projects.length === 0 ? (
        <div className="state">
          <h3>No data yet</h3>
          <p>Create a project and charts will appear here.</p>
          {canCreate && (
            <Link to="/projects" className="btn btn-primary">
              Create project
            </Link>
          )}
        </div>
      ) : (
        <>
          <div className="grid grid-2">
            <div className="card chart-card">
              <h3>Projects by creator</h3>
              <p className="chart-sub">How many projects each member owns.</p>
              <div className="chart-body">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={byCreator} margin={{ top: 8, right: 8, bottom: 4, left: -18 }}>
                    <CartesianGrid stroke="#000000" strokeOpacity={0.12} vertical={false} />
                    <XAxis dataKey="name" stroke="#000000" strokeWidth={1.5} tickLine={false} />
                    <YAxis stroke="#000000" strokeWidth={1.5} tickLine={false} allowDecimals={false} />
                    <Tooltip content={<BrutalTooltip />} cursor={{ fill: "rgba(0,0,0,0.06)" }} />
                    <Bar dataKey="count" name="Projects" fill="#fde047" stroke="#000000" strokeWidth={1.5} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="card chart-card">
              <h3>Ownership share</h3>
              <p className="chart-sub">Distribution of projects per creator.</p>
              <div className="chart-body">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie
                      data={byCreator}
                      dataKey="count"
                      nameKey="name"
                      outerRadius={90}
                      stroke="#000000"
                      strokeWidth={1.5}
                      isAnimationActive={false}
                    >
                      {byCreator.map((entry, i) => (
                        <Cell key={entry.name} fill={PALETTE[i % PALETTE.length]} />
                      ))}
                    </Pie>
                    <Tooltip content={<BrutalTooltip />} />
                  </PieChart>
                </ResponsiveContainer>
              </div>
              <div className="legend-brutal">
                {byCreator.map((entry, i) => (
                  <span className="legend-item" key={entry.name}>
                    <span className="legend-swatch" style={{ background: PALETTE[i % PALETTE.length] }} />
                    {entry.name} ({entry.count})
                  </span>
                ))}
              </div>
            </div>
          </div>

          <div className="grid mt-16">
            <div className="card chart-card">
              <h3>Projects over time</h3>
              <p className="chart-sub">Cumulative number of projects created.</p>
              <div className="chart-body">
                <ResponsiveContainer width="100%" height="100%">
                  <AreaChart data={overTime} margin={{ top: 8, right: 8, bottom: 4, left: -18 }}>
                    <defs>
                      <linearGradient id="areaFill" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#bae6fd" stopOpacity={1} />
                        <stop offset="100%" stopColor="#e9d5ff" stopOpacity={0.85} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid stroke="#000000" strokeOpacity={0.12} vertical={false} />
                    <XAxis dataKey="date" stroke="#000000" strokeWidth={1.5} tickLine={false} />
                    <YAxis stroke="#000000" strokeWidth={1.5} tickLine={false} allowDecimals={false} />
                    <Tooltip content={<BrutalTooltip />} />
                    <Area
                      type="monotone"
                      dataKey="total"
                      name="Total"
                      stroke="#000000"
                      strokeWidth={1.5}
                      fill="url(#areaFill)"
                      isAnimationActive={false}
                    />
                  </AreaChart>
                </ResponsiveContainer>
              </div>
            </div>
          </div>
        </>
      )}

      <div className="section-title mt-24">Quick actions</div>
      <div className="grid grid-2">
        <div className="card">
          <h3 className="mt-0">Projects</h3>
          <p className="muted" style={{ marginTop: 6 }}>
            {canCreate ? "Create new projects and track existing ones." : "Browse projects you have access to."}
          </p>
          <Link to="/projects" className="btn btn-outline mt-16">
            Open projects
          </Link>
        </div>
        <div className="card">
          <h3 className="mt-0">Switch account</h3>
          <p className="muted" style={{ marginTop: 6 }}>
            Log out and sign in with a different user.
          </p>
          <Link to="/login" className="btn btn-outline mt-16">
            Sign in as someone else
          </Link>
        </div>
      </div>
    </Layout>
  );
}
