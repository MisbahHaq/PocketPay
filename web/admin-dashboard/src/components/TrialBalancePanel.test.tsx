import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TrialBalancePanel } from "./TrialBalancePanel";

describe("TrialBalancePanel", () => {
  it("renders BALANCED status when balanced is true", () => {
    render(<TrialBalancePanel balanced={true} difference="0" />);
    expect(screen.getByText("BALANCED")).toBeDefined();
    expect(screen.getByText(/Double-entry invariant holds/i)).toBeDefined();
  });

  it("renders MISMATCH status when balanced is false", () => {
    render(<TrialBalancePanel balanced={false} difference="500.00 PKR" />);
    expect(screen.getByText(/MISMATCH 500.00 PKR/i)).toBeDefined();
    expect(screen.getByText(/Integrity alert/i)).toBeDefined();
  });

  it("renders currency breakdown lines when provided", () => {
    const currencies = [
      { currency: "PKR", totalDebits: 100000, totalCredits: 100000, difference: 0 },
      { currency: "USD", totalDebits: 5000, totalCredits: 5000, difference: 0 },
    ];
    render(<TrialBalancePanel balanced={true} difference="0" currencies={currencies} />);
    expect(screen.getByText("PKR Ledger")).toBeDefined();
    expect(screen.getByText("USD Ledger")).toBeDefined();
  });
});
