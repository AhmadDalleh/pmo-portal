export const CR_TYPES = ['scope', 'schedule', 'budget', 'resource'] as const;

export interface ChangeRequest {
  id: number;
  code: string;
  projectId: number;
  projectCode: string;
  title: string;
  description: string;
  type: string;
  costImpact: number | null;
  scheduleImpactDays: number | null;
  status: 'draft' | 'submitted' | 'returned' | 'approved' | 'rejected' | 'withdrawn';
  requestedById: number;
  requestedByName: string;
  createdAt: string;
  submittedAt: string | null;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionReason: string | null;
  version: number;
}

export interface ChangeRequestBody {
  title: string;
  description: string;
  type: string;
  costImpact: number | null;
  scheduleImpactDays: number | null;
}

export interface ChangeRequestHistoryItem {
  id: number;
  fromStatus: string | null;
  toStatus: string;
  actorId: number;
  actorName: string;
  reason: string | null;
  occurredAt: string;
}
