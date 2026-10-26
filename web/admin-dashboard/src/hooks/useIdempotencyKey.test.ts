import { describe, it, expect } from "vitest";
import { useIdempotencyKey } from "./useIdempotencyKey";
import { renderHook, act } from "@testing-library/react";

describe("useIdempotencyKey", () => {
    it("initializes with null key", () => {
        const { result } = renderHook(() => useIdempotencyKey());
        expect(result.current.key).toBeNull();
    });

    it("generates a valid UUID on ensure() and persists it", () => {
        const { result } = renderHook(() => useIdempotencyKey());
        let generatedKey = "";

        act(() => {
            generatedKey = result.current.ensure();
        });

        expect(generatedKey).toBeDefined();
        expect(generatedKey.length).toBeGreaterThan(10);
        expect(result.current.key).toBe(generatedKey);

        // Repeated call returns identical key
        let secondCall = "";
        act(() => {
            secondCall = result.current.ensure();
        });
        expect(secondCall).toBe(generatedKey);
    });

    it("clears key when reset() is called", () => {
        const { result } = renderHook(() => useIdempotencyKey());

        act(() => {
            result.current.ensure();
        });
        expect(result.current.key).not.toBeNull();

        act(() => {
            result.current.reset();
        });
        expect(result.current.key).toBeNull();
    });
});

