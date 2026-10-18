import { Health } from './project.models';

/** The filters every dashboard call carries. They live in the URL query string. */
export interface DashboardFilter {
  departmentId?: number;
  projectId?: number;
  managerId?: number;
  status?: string;
  from?: string;
  to?: string;
  categoryId?: number;
  asOf?: string;
}

export interface DashboardKpis {
  totalProjects: number;
  activeProjects: number;
  portfolioProgressPct: number | null;
  overdueTasks: number;
  milestonesDueSoon: number;
  openHighRisks: number;
  blockedTasks: number;
  pendingChangeRequests: number;
  currentBudget: number;
  actualSpend: number;
  remainingBudget: number;
  utilization: number | null;
  forecastToComplete: number;
  eac: number;
  projectedVariance: number;
  projectedOverrunProjects: number;
  projectedOverrunAmount: number;
  asOf: string;
}

export interface ProjectFinanceBar {
  projectId: number;
  code: string;
  name: string;
  currentBudget: number;
  actualSpend: number;
  forecastToComplete: number;
  eac: number;
}

export interface OverdueByOwner {
  userId: number | null;
  ownerName: string;
  overdueTasks: number;
}

export interface RiskMatrixCell {
  probability: number;
  impact: number;
  openCount: number;
}

export interface ProjectHealthRow {
  projectId: number;
  code: string;
  name: string;
  status: string;
  progressPct: number | null;
  overdueTasks: number;
  openHighRisks: number;
  utilization: number | null;
  health: Health;
}

export interface DrilldownCost {
  id: number;
  projectCode: string;
  categoryName: string;
  txnDate: string;
  amount: number;
  description: string;
  vendorName: string | null;
  referenceNo: string | null;
}

export interface DrilldownCosts {
  items: DrilldownCost[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalAmount: number;
}

export interface DrilldownTask {
  id: number;
  projectCode: string;
  title: string;
  assigneeName: string;
  status: string;
  priority: string;
  dueDate: string;
  daysOverdue: number;
  progressPct: number;
}

export interface DrilldownRisk {
  id: number;
  projectCode: string;
  type: string;
  title: string;
  ownerName: string;
  probability: number;
  impact: number;
  score: number;
  status: string;
  dueDate: string | null;
}
