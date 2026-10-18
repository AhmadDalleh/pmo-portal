import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { firstValueFrom } from 'rxjs';
import { GovernanceApi } from '../../core/api/governance.api';
import { toApiError } from '../../core/http/api-error';
import { NotificationItem } from '../../core/models/auth.models';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { AsyncState } from '../../shared/async-state/async-state';

/** Story A6: the signed-in user's notifications. Opening one marks it read and follows its link. */
@Component({
  selector: 'app-notifications-page',
  imports: [AsyncState, MatButtonModule, MatIconModule, MatPaginatorModule],
  template: `
    <div class="page">
      <h1>Notifications</h1>
      <app-async-state [status]="status()" [empty]="items().length === 0" emptyText="You have no notifications." [error]="error()" (retry)="load()">
        <ul class="list" aria-label="Notifications">
          @for (n of items(); track n.id) {
            <li [class.unread]="!n.readAt">
              <button type="button" class="item" (click)="open(n)">
                <mat-icon aria-hidden="true">{{ n.readAt ? 'drafts' : 'mark_email_unread' }}</mat-icon>
                <span class="text"><strong>{{ n.title }}</strong>{{ n.readAt ? '' : ' (unread)' }}<br /><span class="muted">{{ n.body }}</span></span>
                <span class="muted when">{{ n.createdAt.slice(0, 16).replace('T', ' ') }}</span>
              </button>
            </li>
          }
        </ul>
        <mat-paginator [length]="total()" [pageSize]="pageSize" [pageIndex]="page() - 1" (page)="onPage($event)" aria-label="Notification pages" />
      </app-async-state>
    </div>
  `,
  styles: `
    .list { list-style: none; margin: 0; padding: 0; }
    li { border-bottom: 1px solid var(--mat-sys-outline-variant); }
    li.unread { background: var(--mat-sys-surface-container-low); }
    .item { all: unset; box-sizing: border-box; width: 100%; display: flex; gap: 12px; align-items: center; padding: 10px 8px; cursor: pointer; }
    .item:focus-visible { outline: 2px solid var(--mat-sys-primary); }
    .text { flex: 1 1 auto; } .when { font-size: 12px; white-space: nowrap; }
  `,
})
export class NotificationsPage {
  private readonly api = inject(GovernanceApi);
  private readonly router = inject(Router);

  protected readonly items = signal<NotificationItem[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = 20;
  protected readonly status = signal<LoadStatus>('idle');
  protected readonly error = signal<ApiError | null>(null);

  constructor() { void this.load(); }

  protected onPage(e: PageEvent): void { this.page.set(e.pageIndex + 1); void this.load(); }

  protected async load(): Promise<void> {
    this.status.set('loading');
    try {
      const result = await firstValueFrom(this.api.notifications(this.page(), this.pageSize));
      this.items.set(result.items);
      this.total.set(result.totalCount);
      this.status.set('idle');
    } catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  protected async open(n: NotificationItem): Promise<void> {
    if (!n.readAt) { try { await firstValueFrom(this.api.markRead(n.id)); } catch { /* following the link matters more */ } }
    if (n.linkUrl) await this.router.navigateByUrl(n.linkUrl);
    else await this.load();
  }
}
