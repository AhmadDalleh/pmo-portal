export const TASK_STATUSES = ['todo', 'in_progress', 'blocked', 'done', 'cancelled'] as const;
export const TASK_PRIORITIES = ['low', 'medium', 'high', 'critical'] as const;

export interface TaskItem {
  id: number;
  projectId: number;
  projectCode: string;
  milestoneId: number | null;
  milestoneName: string | null;
  title: string;
  description: string | null;
  assigneeId: number | null;
  assigneeName: string | null;
  status: string;
  priority: string;
  startDate: string | null;
  dueDate: string;
  weight: number;
  progressPct: number;
  completedAt: string | null;
  /** "overdue", "due_today", "due_soon" or "" - worked out by the server. */
  indicator: string;
  version: number;
}

export interface TaskBody {
  title: string;
  description: string | null;
  assigneeId: number | null;
  milestoneId: number | null;
  status: string;
  priority: string;
  startDate: string | null;
  dueDate: string;
  weight: number;
  progressPct: number;
}

export interface Milestone {
  id: number;
  projectId: number;
  name: string;
  description: string | null;
  dueDate: string;
  status: string;
  completedAt: string | null;
  isOverdue: boolean;
  tasksTotal: number;
  tasksDone: number;
  version: number;
}

export interface Risk {
  id: number;
  projectId: number;
  projectCode: string;
  type: 'risk' | 'issue';
  title: string;
  description: string | null;
  ownerId: number;
  ownerName: string;
  probability: number;
  impact: number;
  score: number;
  isHighExposure: boolean;
  mitigation: string | null;
  dueDate: string | null;
  status: string;
  closedAt: string | null;
  version: number;
}

export interface RiskBody {
  type: string;
  title: string;
  description: string | null;
  ownerId: number;
  probability: number;
  impact: number;
  mitigation: string | null;
  dueDate: string | null;
  status: string;
}

export interface ProgressSnapshot {
  snapshotDate: string;
  progressPct: number;
  tasksTotal: number;
  tasksDone: number;
  actualSpend: number;
}
