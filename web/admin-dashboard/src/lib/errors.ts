export interface ProblemDetails { status?: number; title?: string; code?: string; correlationId?: string; errors?: Record<string,string[]>; }

export function parseProblemDetails(err: unknown): ProblemDetails {
  if (err && typeof err === "object") return err as ProblemDetails;
  return { status: 500, title: "Unknown error" };
}

export const ERROR_MESSAGES: Record<string,string> = {
  RATE_LIMITED: "Too many requests. Please wait and retry.",
  UNAUTHORIZED: "Please sign in again.",
  FORBIDDEN: "You do not have permission.",
  NOT_FOUND: "Resource not found.",
  VALIDATION_FAILED: "Please fix the highlighted fields.",
  BUSINESS_RULE: "This action is not allowed.",
};

export function messageForCode(code?: string): string {
  if (code && ERROR_MESSAGES[code]) return ERROR_MESSAGES[code];
  return "Something went wrong. Please try again.";
}
