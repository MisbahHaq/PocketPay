import { describe, it, expect, beforeEach } from "vitest";
import { useHashRouter } from "./useHashRouter";
import { renderHook, act } from "@testing-library/react";

describe("useHashRouter", () => {
    beforeEach(() => {
        window.location.hash = "";
    });

    it("parses empty hash as root route with empty params", () => {
        const { result } = renderHook(() => useHashRouter());
        expect(result.current.route.path).toBe("");
        expect(result.current.route.params).toEqual({});
    });

    it("navigates and updates path and query parameters", () => {
        const { result } = renderHook(() => useHashRouter());

        act(() => {
            result.current.navigate("transfers", {
                status: "Completed",
                page: 2,
            });
        });

        expect(window.location.hash).toBe(
            "#/transfers?status=Completed&page=2",
        );
        expect(result.current.route.path).toBe("transfers");
        expect(result.current.route.params).toEqual({
            status: "Completed",
            page: "2",
        });
    });

    it("updates individual params without wiping others", () => {
        window.location.hash = "#/users?page=1&search=test";
        const { result } = renderHook(() => useHashRouter());

        act(() => {
            result.current.updateParams({ page: 2 });
        });

        expect(result.current.route.params.page).toBe("2");
        expect(result.current.route.params.search).toBe("test");
    });
});

