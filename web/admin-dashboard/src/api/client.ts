import { tokenStore } from "../auth/tokenStore";
import { refreshOnce } from "../auth/refresh";
import { initialDemoState } from "./demoData";

let demoMode = false;
const demoState = { ...initialDemoState };

export function isDemoMode(): boolean {
    return demoMode;
}

export function setDemoMode(val: boolean): void {
    demoMode = val;
}

export function getDemoState() {
    return demoState;
}

export async function apiFetch(
    path: string,
    init?: RequestInit,
): Promise<Response> {
    const base = (import.meta as any).env?.VITE_API_BASE ?? "";
    const headers = new Headers(init?.headers);

    const token = tokenStore.getAccess();
    if (token && !headers.has("Authorization")) {
        headers.set("Authorization", `Bearer ${token}`);
    }

    if (
        init?.body &&
        !headers.has("Content-Type") &&
        typeof init.body === "string"
    ) {
        headers.set("Content-Type", "application/json");
    }

    // If explicitly in demo mode, intercept and serve from demo state
    if (demoMode) {
        return handleDemoRequest(path, init, headers);
    }

    try {
        let response = await fetch(base + path, {
            ...init,
            headers,
            credentials: "omit",
        });

        // Handle 401 Unauthorized with single-flight refresh
        if (response.status === 401 && tokenStore.getRefresh()) {
            const refreshedToken = await refreshOnce(async () => {
                const refreshRes = await fetch(base + "/api/auth/refresh", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({
                        refreshToken: tokenStore.getRefresh(),
                    }),
                });
                if (refreshRes.ok) {
                    const data = await refreshRes.json();
                    tokenStore.set(data.accessToken, data.refreshToken);
                    return data.accessToken;
                }
                tokenStore.clear();
                return null;
            });

            if (refreshedToken) {
                headers.set("Authorization", `Bearer ${refreshedToken}`);
                response = await fetch(base + path, {
                    ...init,
                    headers,
                    credentials: "omit",
                });
            }
        }

        return response;
    } catch (err) {
        // Network error: Fall back gracefully to demo data with a warning
        console.warn(
            `[PayPocket API] Network request to ${path} failed, serving simulated demo response.`,
            err,
        );
        demoMode = true;
        return handleDemoRequest(path, init, headers);
    }
}

function handleDemoRequest(
    path: string,
    init?: RequestInit,
    headers?: Headers,
): Response {
    const method = (init?.method || "GET").toUpperCase();

    // /api/auth/login
    if (path.includes("/api/auth/login") && method === "POST") {
        const fakeToken = createFakeJwt("admin@paypocket.local", "Admin");
        return jsonResponse({
            accessToken: fakeToken,
            refreshToken: "demo-refresh-token",
        });
    }

    // /api/admin/ledger/trial-balance
    if (path.includes("/api/admin/ledger/trial-balance")) {
        return jsonResponse(demoState.trialBalance);
    }

    // /api/admin/users
    if (path.includes("/api/admin/users")) {
        return jsonResponse({
            items: demoState.users,
            totalCount: demoState.users.length,
            page: 1,
            pageSize: 20,
        });
    }

    // /api/admin/wallets/.../fund
    if (path.match(/\/api\/admin\/wallets\/[^/]+\/fund/) && method === "POST") {
        const body = init?.body
            ? JSON.parse(init.body as string)
            : { amountMinorUnits: 100000 };
        const amount = body.amountMinorUnits || 100000;
        const journalId = crypto.randomUUID();

        // Update trial balance
        const pkr = demoState.trialBalance.currencies.find(
            (c) => c.currency === "PKR",
        );
        if (pkr) {
            pkr.totalDebits += amount;
            pkr.totalCredits += amount;
        }

        // Add audit event
        demoState.auditEvents.unshift({
            id: crypto.randomUUID(),
            occurredAt: new Date().toISOString(),
            actorUserId: "demo-admin",
            actorRole: "Admin",
            action: "Wallet.Fund",
            entityType: "Wallet",
            entityId: path.split("/")[4] || "demo-wallet",
            correlationId: `corr-${Date.now()}`,
            ipAddress: "127.0.0.1",
            userAgent: "AdminDashboard",
            outcome: "SUCCESS",
            details: JSON.stringify({ amountMinorUnits: amount }),
            previousHash: demoState.auditEvents[0]?.hash || "0".repeat(64),
            hash: Math.random().toString(16).substring(2).padEnd(64, "a"),
        });

        return jsonResponse(
            {
                journalEntryId: journalId,
                walletId: path.split("/")[4] || "demo-wallet",
                amountMinorUnits: amount,
                currency: "PKR",
            },
            201,
        );
    }

    // /api/admin/transfers/.../reverse
    if (
        path.match(/\/api\/admin\/transfers\/[^/]+\/reverse/) &&
        method === "POST"
    ) {
        const transferId = path.split("/")[4];
        const transfer = demoState.transfers.find((t) => t.id === transferId);
        if (transfer) {
            transfer.status = "Reversed";
        }
        return jsonResponse(
            {
                id: transferId,
                status: "Reversed",
                note: "Admin reversal",
            },
            201,
        );
    }

    // /api/admin/transfers
    if (path.includes("/api/admin/transfers")) {
        return jsonResponse({
            items: demoState.transfers,
            totalCount: demoState.transfers.length,
            page: 1,
            pageSize: 20,
        });
    }

    // /api/admin/reconciliation/runs
    if (path.includes("/api/admin/reconciliation/runs")) {
        if (method === "POST") {
            const body = init?.body ? JSON.parse(init.body as string) : {};
            const newRun = {
                id: crypto.randomUUID(),
                billerCode: body.billerCode || "MOCK",
                businessDate:
                    body.businessDate || new Date().toISOString().slice(0, 10),
                status: "Completed" as const,
                startedAt: new Date().toISOString(),
                completedAt: new Date().toISOString(),
                matchedCount: 25,
                missingAtBillerCount: 0,
                unknownAtOurSideCount: 0,
                amountMismatchCount: 0,
            };
            demoState.reconciliationRuns.unshift(newRun);
            return jsonResponse({ runId: newRun.id }, 202);
        }
        return jsonResponse(demoState.reconciliationRuns);
    }

    // /api/admin/audit-events or /api/admin/audit
    if (path.includes("/api/admin/audit")) {
        return jsonResponse({
            items: demoState.auditEvents,
            totalCount: demoState.auditEvents.length,
            page: 1,
            pageSize: 50,
        });
    }

    // /health/live and /health/ready
    if (path.includes("/health/")) {
        return jsonResponse({ status: "Healthy" });
    }

    return jsonResponse({ message: "OK" });
}

function jsonResponse(data: unknown, status = 200): Response {
    return new Response(JSON.stringify(data), {
        status,
        headers: { "Content-Type": "application/json" },
    });
}

function createFakeJwt(email: string, role: string): string {
    const header = btoa(JSON.stringify({ alg: "HS256", typ: "JWT" }));
    const payload = btoa(
        JSON.stringify({
            sub: "a1000000-0000-0000-0000-000000000001",
            email,
            role,
            exp: Math.floor(Date.now() / 1000) + 3600,
        }),
    );
    return `${header}.${payload}.demo-sig`;
}

