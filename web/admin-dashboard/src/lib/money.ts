export function formatMoney(minorUnits: bigint | number, currency: string): string {
  const bi = typeof minorUnits === "bigint" ? minorUnits : BigInt(minorUnits);
  const decimals = 2;
  const scale = BigInt(10) ** BigInt(decimals);
  const neg = bi < 0n;
  const abs = neg ? -bi : bi;
  const whole = abs / scale;
  const frac = abs % scale;
  const fracStr = frac.toString().padStart(decimals, "0");
  return `${neg ? "-" : ""}${whole.toLocaleString("en-PK")}.${fracStr} ${currency}`;
}

export function parseMoneyInput(input: string, currency: string): bigint {
  const trimmed = input.trim().replace(/,/g, "");
  if (!/^\d+(\.\d+)?$/.test(trimmed)) throw new Error("invalid amount");
  const [w, f = ""] = trimmed.split(".");
  if (f.length > 2) throw new Error("too many decimals");
  const frac = (f + "00").slice(0, 2);
  return BigInt(w) * 100n + BigInt(frac || "0");
}
