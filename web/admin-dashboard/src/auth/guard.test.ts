import { describe, it, expect, beforeEach } from "vitest";
import { isAdmin } from "./guard";
import { tokenStore } from "./tokenStore";

function createJwt(payload: Record<string, unknown>): string {
    const header = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
    const body = btoa(JSON.stringify(payload));
    return `${header}.${body}.signature`;
}

describe("guard (isAdmin)", () => {
    beforeEach(() => {
        tokenStore.clear();
    });

    it("returns false when no token is present", () => {
        expect(isAdmin()).toBe(false);
    });

    it("returns true when token has role 'Admin'", () => {
        const token = createJwt({
            sub: "user-1",
            role: "Admin",
            email: "admin@paypocket.local",
        });
        tokenStore.set(token, "refresh-1");
        expect(isAdmin()).toBe(true);
    });

    it("returns false when token has role 'Customer'", () => {
        const token = createJwt({
            sub: "user-2",
            role: "Customer",
            email: "customer@paypocket.local",
        });
        tokenStore.set(token, "refresh-2");
        expect(isAdmin()).toBe(false);
    });

    it("returns false when token is malformed", () => {
        tokenStore.set("invalid-jwt-token", "refresh-3");
        expect(isAdmin()).toBe(false);
    });
});

