import { describe, it, expect, vi } from "vitest";
import { refreshOnce } from "./refresh";

describe("refreshOnce (single-flight refresh)", () => {
    it("coalesces concurrent refresh calls into a single flight", async () => {
        let callCount = 0;
        const mockRefresh = vi.fn(async () => {
            callCount++;
            // Simulate network latency
            await new Promise((resolve) => setTimeout(resolve, 20));
            return "new-token-123";
        });

        // Fire 5 concurrent refresh calls
        const p1 = refreshOnce(mockRefresh);
        const p2 = refreshOnce(mockRefresh);
        const p3 = refreshOnce(mockRefresh);
        const p4 = refreshOnce(mockRefresh);
        const p5 = refreshOnce(mockRefresh);

        const results = await Promise.all([p1, p2, p3, p4, p5]);

        expect(callCount).toBe(1);
        expect(mockRefresh).toHaveBeenCalledTimes(1);
        expect(results).toEqual([
            "new-token-123",
            "new-token-123",
            "new-token-123",
            "new-token-123",
            "new-token-123",
        ]);
    });

    it("resets inflight after completion so subsequent calls can trigger a new refresh", async () => {
        let counter = 0;
        const mockRefresh = vi.fn(async () => {
            counter++;
            return `token-${counter}`;
        });

        const first = await refreshOnce(mockRefresh);
        expect(first).toBe("token-1");

        const second = await refreshOnce(mockRefresh);
        expect(second).toBe("token-2");
        expect(mockRefresh).toHaveBeenCalledTimes(2);
    });
});

