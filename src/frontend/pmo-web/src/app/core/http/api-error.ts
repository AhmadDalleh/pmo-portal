import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from '../models/common.models';

/** Turns any failed request into an ApiError (RFC 7807 ProblemDetails fields, plus the correlation id header). */
export function toApiError(e: unknown): ApiError {
  if (e instanceof HttpErrorResponse) {
    const body = (typeof e.error === 'object' && e.error !== null ? e.error : {}) as Partial<ApiError> & { errors?: Record<string, string[]> };
    return {
      status: e.status,
      code: body.code,
      title: body.title,
      detail: e.status === 0 ? 'The server could not be reached.' : (body.detail ?? e.statusText),
      errors: body.errors,
      correlationId: body.correlationId ?? e.headers?.get('X-Correlation-Id') ?? undefined,
    };
  }
  if (typeof e === 'object' && e !== null && 'status' in e) {
    return e as ApiError;   // already normalised by the interceptor
  }
  return { status: 0, detail: e instanceof Error ? e.message : 'Unexpected error.' };
}
