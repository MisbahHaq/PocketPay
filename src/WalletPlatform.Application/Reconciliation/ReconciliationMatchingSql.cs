namespace WalletPlatform.Application.Reconciliation;

public static class ReconciliationMatchingSql
{
    public const string FullOuterJoin = @"
SELECT COALESCE(o.ClientReference, b.ClientReference) AS ClientReference,
       o.PaymentId, o.OurAmount, o.OurStatus,
       b.BillerAmount, b.BillerStatus, b.DuplicateCount
FROM (SELECT ClientReference, PaymentId, AmountMinorUnits AS OurAmount, Status AS OurStatus
      FROM payments_for_run) o
FULL OUTER JOIN (SELECT ClientReference, AmountMinorUnits AS BillerAmount, Status AS BillerStatus,
                        COUNT(*) OVER (PARTITION BY ClientReference) AS DuplicateCount
                 FROM settlement_rows WHERE SettlementFileId = @fileId) b
  ON o.ClientReference = b.ClientReference;";
}
