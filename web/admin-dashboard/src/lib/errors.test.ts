import { describe, it, expect } from "vitest";
import { parseProblemDetails, messageForCode } from "./errors";

describe("errors", () => {
    it("parses known ProblemDetails object", () => {
        const details = {
            status: 400,
            title: "Validation error",
            code: "VALIDATION_FAILED",
            errors: { amount: ["Amount must be greater than 0"] },
        };
        const parsed = parseProblemDetails(details);
        expect(parsed.status).toBe(400);
        expect(parsed.code).toBe("VALIDATION_FAILED");
    });

    it("provides fallback for unknown error object", () => {
        const parsed = parseProblemDetails(null);
        expect(parsed.status).toBe(500);
        expect(parsed.title).toBe("Unknown error");
    });

    it("maps known error codes to friendly human messages", () => {
        expect(messageForCode("RATE_LIMITED")).toBe(
            "Too many requests. Please wait and retry.",
        );
        expect(messageForCode("UNAUTHORIZED")).toBe("Please sign in again.");
        expect(messageForCode("UNKNOWN_CODE")).toBe(
            "Something went wrong. Please try again.",
        );
    });
});

