export type Health = 'red' | 'amber' | 'green';

export interface ProjectListItem {
  id: number;
  code: string;
  name: string;
  departmentName: string;
  ownerName: string;
  status: string;
  startDate: string;
  plannedEndDate: string;
  progressPct: number | null;
  utilization: number | null;
  health: Health;
  nearBudget: boolean;
  overspent: boolean;
  projectedOverrun: boolean;
  noForecast: boolean;
}

export interface Project {
  id: number;
  code: string;
  name: string;
  description: string | null;
  departmentId: number;
  departmentName: string;
  ownerId: number;
  ownerName: string;
  status: string;
  startDate: string;
  plannedEndDate: string;
  actualEndDate: string | null;
  initialBudget: number;
  version: number;
}

export interface ProjectSummary {
  projectId: number;
  asOf: string;
  health: Health;
  delivery: { progressPct: number | null; overdueTasks: number; nextMilestone: { id: number; name: string; dueDate: string; isOverdue: boolean } | null };
  governance: { openHighExposureRisks: number; pendingChangeRequests: number };
  finance: FinanceFigures;
}

export interface FinanceFigures {
  currentBudget: number;
  actualSpend: number;
  remainingBudget: number;
  utilization: number | null;
  forecastToComplete: number;
  eac: number;
  projectedVariance: number;
  nearBudget: boolean;
  overspent: boolean;
  projectedOverrun: boolean;
  noForecast: boolean;
  noBudget: boolean;
}

export interface Member {
  userId: number;
  fullName: string;
  email: string;
  userRole: string;
  memberRole: 'manager' | 'contributor';
  addedAt: string;
}

export interface ProjectQuery {
  search?: string;
  departmentId?: number;
  ownerId?: number;
  status?: string;
  sort: string;
  dir: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export const PROJECT_STATUSES = ['proposed', 'active', 'on_hold', 'completed', 'cancelled'] as const;
