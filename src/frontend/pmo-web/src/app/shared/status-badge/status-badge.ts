import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

interface BadgeStyle { icon: string; label: string; tone: 'good' | 'warn' | 'bad' | 'info' | 'neutral' }

const STYLES: Record<string, BadgeStyle> = {
  // project health
  green: { icon: 'check_circle', label: 'On track', tone: 'good' },
  amber: { icon: 'warning', label: 'At risk', tone: 'warn' },
  red: { icon: 'error', label: 'Off track', tone: 'bad' },
  // project / task / milestone / risk / cost / change request / revision statuses
  proposed: { icon: 'lightbulb', label: 'Proposed', tone: 'info' },
  active: { icon: 'play_circle', label: 'Active', tone: 'good' },
  on_hold: { icon: 'pause_circle', label: 'On hold', tone: 'warn' },
  completed: { icon: 'task_alt', label: 'Completed', tone: 'good' },
  cancelled: { icon: 'cancel', label: 'Cancelled', tone: 'neutral' },
  todo: { icon: 'radio_button_unchecked', label: 'To do', tone: 'neutral' },
  in_progress: { icon: 'autorenew', label: 'In progress', tone: 'info' },
  blocked: { icon: 'block', label: 'Blocked', tone: 'bad' },
  done: { icon: 'check_circle', label: 'Done', tone: 'good' },
  pending: { icon: 'schedule', label: 'Pending', tone: 'warn' },
  open: { icon: 'report', label: 'Open', tone: 'warn' },
  mitigating: { icon: 'build_circle', label: 'Mitigating', tone: 'info' },
  closed: { icon: 'lock', label: 'Closed', tone: 'neutral' },
  draft: { icon: 'edit_note', label: 'Draft', tone: 'neutral' },
  posted: { icon: 'check_circle', label: 'Posted', tone: 'good' },
  submitted: { icon: 'send', label: 'Submitted', tone: 'info' },
  returned: { icon: 'undo', label: 'Returned', tone: 'warn' },
  approved: { icon: 'thumb_up', label: 'Approved', tone: 'good' },
  rejected: { icon: 'thumb_down', label: 'Rejected', tone: 'bad' },
  withdrawn: { icon: 'remove_circle_outline', label: 'Withdrawn', tone: 'neutral' },
  // task date indicators
  overdue: { icon: 'alarm', label: 'Overdue', tone: 'bad' },
  due_today: { icon: 'today', label: 'Due today', tone: 'warn' },
  due_soon: { icon: 'event', label: 'Due soon', tone: 'info' },
};

/** Status as icon + text + colour. Colour alone is never the signal (accessibility). */
@Component({
  selector: 'app-status-badge',
  imports: [MatIconModule],
  template: `<span class="badge {{ style().tone }}"><mat-icon aria-hidden="true">{{ style().icon }}</mat-icon>{{ style().label }}</span>`,
  styles: `
    .badge { display: inline-flex; align-items: center; gap: 4px; padding: 2px 10px 2px 6px; border-radius: 12px; font-size: 12px; font-weight: 500; white-space: nowrap; }
    mat-icon { font-size: 16px; width: 16px; height: 16px; }
    .good { background: #dff3e3; color: #14532d; }
    .warn { background: #fdecc8; color: #6b4200; }
    .bad { background: #fbdada; color: #7f1d1d; }
    .info { background: #dbeafe; color: #1e3a8a; }
    .neutral { background: #e7e7ea; color: #3b3b40; }
  `,
})
export class StatusBadge {
  readonly value = input.required<string>();
  protected readonly style = computed<BadgeStyle>(() => STYLES[this.value()] ?? { icon: 'help_outline', label: this.value(), tone: 'neutral' });
}
