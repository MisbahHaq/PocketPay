import { tokenStore } from "./tokenStore";

export function isAdmin(): boolean {
  const t = tokenStore.getAccess();
  if (!t) return false;
  try {
    const p = JSON.parse(atob(t.split(".")[1]));
    return p.role === "Admin";
  } catch {
    return false;
  }
}

export interface UserSession {
  email: string;
  role: string;
  sub?: string;
  exp?: number;
}

export function getSessionUser(): UserSession | null {
  const t = tokenStore.getAccess();
  if (!t) return null;
  try {
    const p = JSON.parse(atob(t.split(".")[1]));
    return {
      email: p.email || p.sub || "admin@paypocket.local",
      role: p.role || "Admin",
      sub: p.sub,
      exp: p.exp,
    };
  } catch {
    return null;
  }
}
