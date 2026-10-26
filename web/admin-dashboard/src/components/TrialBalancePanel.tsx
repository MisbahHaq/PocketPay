export function TrialBalancePanel({ balanced, difference }: { balanced: boolean; difference: string }) {
  return (
    <div className={balanced ? "text-green-600" : "text-red-600 font-bold"}>
      {balanced ? "BALANCED" : `MISMATCH ${difference}`}
    </div>
  );
}
