export interface UserSummary {
  id: string;
  email: string;
  role: string;
  createdAtUtc: string;
  isLockedOut: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface FundWalletRequest {
  amountMinorUnits: number;
}

export interface FundWalletResponse {
  journalEntryId: string;
  walletId: string;
  amountMinorUnits: number;
  currency: string;
}

export interface TrialBalanceCurrencyLine {
  currency: string;
  totalDebits: number;
  totalCredits: number;
  difference: number;
}

export interface TrialBalanceResponse {
  isBalanced: boolean;
  currencies: TrialBalanceCurrencyLine[];
}

export type TransferStatus = 'Pending' | 'Completed' | 'Failed' | 'Reversed';

export interface TransferSummary {
  id: string;
  sourceWalletId: string;
  destinationWalletId: string;
  amountMinorUnits: number;
  feeMinorUnits: number;
  currency: string;
  status: TransferStatus | number;
  note?: string | null;
  createdAt: string;
  completedAt?: string | null;
  failureReason?: string | null;
}

export interface TransfersPageResponse {
  items: TransferSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TransferDetail extends TransferSummary {
  journalEntryId?: string | null;
}

export interface ReconciliationRun {
  id: string;
  billerCode: string;
  businessDate: string;
  settlementFileId?: string | null;
  status: 'Running' | 'Completed' | 'Failed' | number;
  startedAt: string;
  completedAt?: string | null;
  matchedCount: number;
  missingAtBillerCount: number;
  unknownAtOurSideCount: number;
  amountMismatchCount: number;
}

export interface AuditEvent {
  id: string;
  occurredAt: string;
  actorUserId?: string | null;
  actorRole?: string | null;
  action: string;
  entityType: string;
  entityId: string;
  correlationId?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  outcome: string;
  details: string;
  previousHash?: string | null;
  hash: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
}
