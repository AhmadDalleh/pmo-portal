import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LoginResponse, Me } from '../models/auth.models';

/**
 * Who is signed in. The access token lives only in memory (a signal): it is gone on reload, and the HttpOnly refresh
 * cookie brings it back (restore). Role helpers here only decide what to SHOW; the server checks every action.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly token = signal<string | null>(null);
  readonly user = signal<Me | null>(null);
  readonly isAuthenticated = computed(() => this.user() !== null);
  readonly isAdmin = computed(() => this.user()?.role === 'Administrator');
  /** Project Manager or Administrator: sees the approvals and can create projects. */
  readonly isManager = computed(() => this.user()?.role === 'ProjectManager' || this.isAdmin());

  private refreshing: Promise<boolean> | null = null;

  canManage(projectId: number): boolean {
    return this.isAdmin() || (this.user()?.managedProjectIds.includes(projectId) ?? false);
  }

  async login(email: string, password: string): Promise<void> {
    this.accept(await firstValueFrom(this.http.post<LoginResponse>('/api/auth/login', { email, password })));
  }

  /** Called once at startup: if the refresh cookie is still valid the user stays signed in after a page reload. */
  async restore(): Promise<void> {
    await this.refresh();
  }

  /** One refresh at a time, however many requests failed with 401 together. */
  refresh(): Promise<boolean> {
    this.refreshing ??= firstValueFrom(this.http.post<LoginResponse>('/api/auth/refresh', {}))
      .then((r) => { this.accept(r); return true; })
      .catch(() => false)
      .finally(() => { this.refreshing = null; });
    return this.refreshing;
  }

  async logout(): Promise<void> {
    try { await firstValueFrom(this.http.post('/api/auth/logout', {})); } catch { /* signing out locally is enough */ }
    this.signOutLocally();
  }

  signOutLocally(): void {
    this.token.set(null);
    this.user.set(null);
    void this.router.navigate(['/login']);
  }

  private accept(r: LoginResponse): void {
    this.token.set(r.accessToken);
    this.user.set(r.user);
  }
}
