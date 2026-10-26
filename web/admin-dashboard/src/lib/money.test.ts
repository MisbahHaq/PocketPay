import { describe, it, expect } from "vitest";
import { formatMoney, parseMoneyInput } from "./money";

describe("money", () => {
  it("formats", () => expect(formatMoney(12345n, "PKR")).toContain("PKR"));
  it("parses", () => expect(parseMoneyInput("12.34", "PKR")).toBe(1234n));
  it("rejects 0.005", () => expect(() => parseMoneyInput("0.005", "PKR")).toThrow());
});
