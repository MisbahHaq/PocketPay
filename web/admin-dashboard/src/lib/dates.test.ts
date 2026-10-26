import { describe, it, expect } from "vitest";
import { toPktDate, formatPktDateTime } from "./dates";

describe("dates (PKT formatting)", () => {
    it("formats ISO string to PKT date in YYYY-MM-DD", () => {
        // 2026-10-26T20:00:00Z is 2026-10-27T01:00:00 in UTC+5 (Asia/Karachi)
        const result = toPktDate("2026-10-26T20:00:00Z");
        expect(result).toBe("2026-10-27");
    });

    it("formats ISO string with PKT suffix", () => {
        const formatted = formatPktDateTime("2026-10-26T12:00:00Z");
        expect(formatted).toContain("PKT");
    });
});

