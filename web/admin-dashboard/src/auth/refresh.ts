let inflight: Promise<string | null> | null = null;
export async function refreshOnce(refreshFn: () => Promise<string | null>): Promise<string | null> {
  if (inflight) return inflight;
  inflight = refreshFn().finally(() => { inflight = null; });
  return inflight;
}
