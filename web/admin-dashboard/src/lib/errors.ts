export interface ProblemDetails { status?: number; title?: string; code?: string; correlationId?: string; errors?: Record<string,string[]>; }

export function parseProblemDetails(err: unknown): ProblemDetails {
  if (err && typeof err === "object") return err as ProblemDetails;
  return { status: 500, title: "Unknown error" };
}

export const ERROR_MESSAGES: Record<string,string> = {
  RATE_LIMITED: "Too many requests. Please wait.",
};
