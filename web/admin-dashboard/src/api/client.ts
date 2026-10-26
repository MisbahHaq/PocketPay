export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  const base = (import.meta as any).env?.VITE_API_BASE ?? "";
  const headers = new Headers(init?.headers);
  return fetch(base + path, { ...init, headers, credentials: "omit" });
}
