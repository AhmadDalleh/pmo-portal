import { HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { toApiError } from './api-error';

/** Outermost interceptor: whatever fails (after a possible token refresh) reaches the screens as an ApiError. */
export const errorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(catchError((e) => throwError(() => toApiError(e))));
