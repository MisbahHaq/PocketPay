import { useState, useEffect, useCallback } from "react";

export interface ParsedRoute {
    path: string;
    params: Record<string, string>;
}

function parseHash(hash: string): ParsedRoute {
    const clean = hash.replace(/^#\/?/, "");
    const [pathPart, queryPart] = clean.split("?");
    const path = pathPart || "";
    const params: Record<string, string> = {};

    if (queryPart) {
        const searchParams = new URLSearchParams(queryPart);
        searchParams.forEach((val, key) => {
            params[key] = val;
        });
    }

    return { path, params };
}

export function useHashRouter() {
    const [route, setRoute] = useState<ParsedRoute>(() =>
        parseHash(window.location.hash),
    );

    useEffect(() => {
        const onHashChange = () => {
            setRoute(parseHash(window.location.hash));
        };
        window.addEventListener("hashchange", onHashChange);
        return () => window.removeEventListener("hashchange", onHashChange);
    }, []);

    const navigate = useCallback(
        (
            path: string,
            params?: Record<string, string | number | undefined | null>,
        ) => {
            let full = `#/${path.replace(/^\//, "")}`;
            if (params) {
                const search = new URLSearchParams();
                Object.entries(params).forEach(([k, v]) => {
                    if (v !== undefined && v !== null && v !== "") {
                        search.set(k, String(v));
                    }
                });
                const qs = search.toString();
                if (qs) {
                    full += `?${qs}`;
                }
            }
            window.location.hash = full;
            setRoute(parseHash(full));
        },
        [],
    );

    const updateParams = useCallback(
        (newParams: Record<string, string | number | undefined | null>) => {
            const current = parseHash(window.location.hash);
            const merged = { ...current.params, ...newParams };
            navigate(current.path, merged);
        },
        [navigate],
    );

    return { route, navigate, updateParams };
}

