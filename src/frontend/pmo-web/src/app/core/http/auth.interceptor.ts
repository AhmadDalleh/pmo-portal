import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

const isAuthCall = (url: string) => url.startsWith('/api/auth/login') || url.startsWith('/api/auth/refresh') || url.startsWith('/api/auth/logout');

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

/**
 * Attaches the in-memory access token. On a 401 it asks the server for a new token once (the refresh cookie does the work),
 * retries the request, and signs the user out if that fails too.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  if (!req.url.startsWith('/api') || isAuthCall(req.url)) {
    return next(req);
  }

  return next(withToken(req, auth.token())).pipe(
    catchError((e: unknown) => {
      if (!(e instanceof HttpErrorResponse) || e.status !== 401) {
        return throwError(() => e);
      }
      return from(auth.refresh()).pipe(
        switchMap((ok) => {
          if (!ok) {
            auth.signOutLocally();
            return throwError(() => e);
          }
          return next(withToken(req, auth.token()));
        }),
      );
    }),
  );
};
