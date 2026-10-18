import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatBadgeModule } from '@angular/material/badge';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatToolbarModule } from '@angular/material/toolbar';
import { filter } from 'rxjs';
import { firstValueFrom } from 'rxjs';
import { GovernanceApi } from './core/api/governance.api';
import { AuthService } from './core/auth/auth.service';

/**
 * The shell: toolbar, role-based menu (A2) and the notification bell. Hiding a menu item is cosmetic;
 * the API refuses what the role may not do.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule, MatIconModule, MatMenuModule, MatBadgeModule],
  template: `
    @if (auth.isAuthenticated()) {
      <mat-toolbar class="bar">
        <a routerLink="/projects" class="brand" aria-label="PMO Portal home"><mat-icon aria-hidden="true">insights</mat-icon> PMO Portal</a>
        <nav class="links" aria-label="Main">
          @for (item of menu(); track item.path) {
            <a mat-button [routerLink]="item.path" routerLinkActive="active" ariaCurrentWhenActive="page"><mat-icon aria-hidden="true">{{ item.icon }}</mat-icon> {{ item.label }}</a>
          }
        </nav>
        <span class="spacer"></span>
        <a mat-icon-button routerLink="/notifications" [attr.aria-label]="'Notifications, ' + unread() + ' unread'">
          <mat-icon [matBadge]="unread() || null" matBadgeSize="small" matBadgeColor="warn" aria-hidden="true">notifications</mat-icon>
        </a>
        <button mat-button type="button" [matMenuTriggerFor]="userMenu"><mat-icon aria-hidden="true">account_circle</mat-icon> {{ auth.user()?.fullName }}</button>
        <mat-menu #userMenu="matMenu">
          <div class="who" tabindex="-1">{{ auth.user()?.role }}<br /><span class="muted">{{ auth.user()?.email }}</span></div>
          <button mat-menu-item type="button" (click)="auth.logout()"><mat-icon aria-hidden="true">logout</mat-icon> Sign out</button>
        </mat-menu>
      </mat-toolbar>
    }
    <main><router-outlet /></main>
  `,
  styles: `
    .bar { gap: 4px; flex-wrap: wrap; height: auto; min-height: 56px; padding: 4px 12px; background: var(--mat-sys-primary); color: var(--mat-sys-on-primary); }
    .bar a, .bar button { color: inherit; }
    .brand { display: inline-flex; align-items: center; gap: 6px; font-weight: 600; text-decoration: none; margin-right: 12px; font-size: 18px; }
    .links { display: flex; flex-wrap: wrap; }
    .links a.active { background: rgb(255 255 255 / 0.18); }
    .spacer { flex: 1 1 auto; }
    .who { padding: 8px 16px; font-weight: 500; }
    .muted { font-weight: 400; font-size: 12px; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  private readonly governance = inject(GovernanceApi);
  private readonly router = inject(Router);

  protected readonly unread = signal(0);

  protected readonly menu = computed(() => {
    const items = [
      { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
      { path: '/projects', label: 'Projects', icon: 'folder_open' },
      { path: '/my-work', label: 'My work', icon: 'task_alt' },
    ];
    const withApprovals = this.auth.isManager() ? [...items, { path: '/approvals', label: 'Approvals', icon: 'fact_check' }] : items;
    return this.auth.isAdmin() ? [...withApprovals, { path: '/admin', label: 'Admin', icon: 'admin_panel_settings' }] : withApprovals;
  });

  constructor() {
    // The bell count is read from the server after every navigation (a mark-as-read or a new notification shows up at once).
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => void this.refreshUnread());
    effect(() => { if (this.auth.isAuthenticated()) untracked(() => void this.refreshUnread()); });
  }

  private async refreshUnread(): Promise<void> {
    if (!this.auth.isAuthenticated()) { this.unread.set(0); return; }
    try { this.unread.set(await firstValueFrom(this.governance.unreadCount())); } catch { /* the bell is not worth an error message */ }
  }
}
