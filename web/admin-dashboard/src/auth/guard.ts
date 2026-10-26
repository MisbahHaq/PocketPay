import { tokenStore } from "./tokenStore";
export function isAdmin(): boolean {
  const t = tokenStore.getAccess();
  if (!t) return false;
  try { const p = JSON.parse(atob(t.split(".")[1])); return p.role === "Admin"; } catch { return false; }
}
