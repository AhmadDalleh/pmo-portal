/** Shapes shared by every API: paging, errors. Dates travel as ISO strings ("2026-09-30"); money as numbers (AED). */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** What the error interceptor turns every failed request into (RFC 7807 ProblemDetails, normalised). */
export interface ApiError {
  status: number;
  code?: string;
  title?: string;
  detail?: string;
  /** Field name (camelCase) to messages, present on validation errors. */
  errors?: Record<string, string[]>;
  correlationId?: string;
}

export interface LookupItem {
  id: number;
  code: string;
  name: string;
}

export interface UserLookup {
  id: number;
  fullName: string;
  email: string;
  role: string;
  departmentId: number;
}

export type LoadStatus = 'idle' | 'loading' | 'error';
