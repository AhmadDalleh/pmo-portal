import { HttpInterceptorFn } from '@angular/common/http';

/** Gives every API call an X-Correlation-Id so an error shown on screen can be found in the server log. */
export const correlationInterceptor: HttpInterceptorFn = (req, next) =>
  req.url.startsWith('/api') ? next(req.clone({ setHeaders: { 'X-Correlation-Id': crypto.randomUUID().replaceAll('-', '') } })) : next(req);
