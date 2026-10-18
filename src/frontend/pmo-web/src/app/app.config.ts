import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideEchartsCore } from 'ngx-echarts';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { echarts } from './core/charts';
import { authInterceptor } from './core/http/auth.interceptor';
import { correlationInterceptor } from './core/http/correlation.interceptor';
import { errorInterceptor } from './core/http/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: the first interceptor is the outermost. Errors are normalised last, after a possible token refresh.
    provideHttpClient(withInterceptors([errorInterceptor, correlationInterceptor, authInterceptor])),
    provideEchartsCore({ echarts }),
    // If the refresh cookie is still valid the user stays signed in after a reload.
    provideAppInitializer(() => inject(AuthService).restore()),
  ],
};
