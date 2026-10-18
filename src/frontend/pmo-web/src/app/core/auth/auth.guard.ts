import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Sends anonymous visitors to the login page and brings them back afterwards. UX only: the API is the real gate. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/** Hides pages the role cannot use (Project Manager / Administrator only). The API refuses the calls anyway. */
export const managerGuard: CanActivateFn = () => (inject(AuthService).isManager() ? true : inject(Router).createUrlTree(['/projects']));

/** Administrator only. The menu entry is hidden for everyone else and the API refuses the calls anyway. */
export const adminGuard: CanActivateFn = () => (inject(AuthService).isAdmin() ? true : inject(Router).createUrlTree(['/projects']));
