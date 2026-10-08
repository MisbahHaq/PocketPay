export function getErrorMessage(err: unknown, fallback: string): string {
  const error = err as { response?: { status?: number; data?: unknown } };
  const status = error?.response?.status;
  const data = error?.response?.data;

  if (status === 401) return "Your session has expired. Please log in again.";
  if (status === 403) return "You need the Manager or Admin role to do that.";
  if (status === 404) return "Not found.";

  if (typeof data === "string" && data.trim()) return data;

  if (data && typeof data === "object") {
    const body = data as { title?: string; message?: string };
    if (body.title?.trim()) return body.title;
    if (body.message?.trim()) return body.message;
  }

  if (error?.response) return fallback;
  return "Cannot reach the server. Make sure the backend is running on port 5155.";
}
