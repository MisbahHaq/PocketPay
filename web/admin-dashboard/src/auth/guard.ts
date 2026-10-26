import { tokenStore } from "./tokenStore";

export function isAdmin(): boolean {
  const t = tokenStore.getAccess();
  if (!t) return false;
  try {
    const payload = JSON.parse(atob(t.split(".")[1]));
    return payload.role === "Admin";
  } catch {
    return false;
  }
}
