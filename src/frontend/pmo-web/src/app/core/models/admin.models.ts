export interface AdminUser {
  id: number;
  email: string;
  fullName: string;
  departmentId: number;
  jobTitle: string | null;
  role: 'Administrator' | 'ProjectManager' | 'Contributor';
  isActive: boolean;
}

export interface AuditEvent {
  id: number;
  occurredAt: string;
  actorId: number | null;
  actorName: string | null;
  projectId: number | null;
  entityType: string;
  entityId: string;
  action: string;
  correlationId: string | null;
  /** JSON text (before/after or key facts, never secrets). */
  data: string | null;
}

export interface AuditQuery {
  entityType?: string;
  action?: string;
  actorId?: number;
  projectId?: number;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}

export interface AutomationRun {
  id: number;
  jobName: string;
  startedAt: string;
  finishedAt: string | null;
  status: string;
  messagesProcessed: number;
  messagesFailed: number;
  error: string | null;
  correlationId: string | null;
}

export interface OutboxMessage {
  id: number;
  type: string;
  payload: string;
  dedupKey: string;
  status: 'pending' | 'processed' | 'failed';
  occurredAt: string;
  availableAt: string;
  attempts: number;
  processedAt: string | null;
  lastError: string | null;
}

export const ROLES = ['Administrator', 'ProjectManager', 'Contributor'] as const;
