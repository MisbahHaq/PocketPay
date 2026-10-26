export function toPktDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-CA", { timeZone: "Asia/Karachi" });
}
export function formatPktDateTime(iso: string): string {
  return new Date(iso).toLocaleString("en-GB", { timeZone: "Asia/Karachi" }) + " PKT";
}
