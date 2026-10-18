import { FinanceFigures } from './project.models';

export interface FinanceSummary extends FinanceFigures {
  projectId: number;
  asOf: string;
  currency: string;
  initialBudget: number;
  approvedRevisions: number;
  pendingRevisions: number;
}

export interface Cost {
  id: number;
  projectId: number;
  categoryId: number;
  categoryName: string;
  txnDate: string;
  amount: number;
  description: string;
  vendorName: string | null;
  referenceNo: string | null;
  status: 'draft' | 'posted';
  reversesTransactionId: number | null;
  isReversed: boolean;
  createdByName: string;
  postedByName: string | null;
  postedAt: string | null;
  version: number;
}

export interface CostBody {
  categoryId: number;
  txnDate: string;
  amount: number;
  description: string;
  vendorName: string | null;
  referenceNo: string | null;
}

export interface BudgetRevision {
  id: number;
  projectId: number;
  amount: number;
  reason: string;
  effectiveDate: string;
  status: 'pending' | 'approved' | 'rejected';
  changeRequestId: number | null;
  requestedByName: string;
  requestedAt: string;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionReason: string | null;
  version: number;
}

export interface ForecastCell {
  categoryId: number;
  categoryName: string;
  periodMonth: string;
  amount: number;
}

export interface CategorySpend {
  categoryId: number;
  code: string;
  name: string;
  actualSpend: number;
  forecastToComplete: number;
}

export interface MonthlyPoint {
  month: string;
  actualSpend: number;
  forecast: number;
}

export interface CostCategory {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
}
