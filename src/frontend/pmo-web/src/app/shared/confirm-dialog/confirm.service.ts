import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { ConfirmData, ConfirmDialog, ConfirmResult } from './confirm-dialog';

/** One line to ask before a consequential action: post, reverse, decide, delete, change role. */
@Injectable({ providedIn: 'root' })
export class Confirm {
  private readonly dialog = inject(MatDialog);

  /** Resolves to false when cancelled, true when confirmed, or the typed reason when `reasonLabel` is given. */
  async ask(data: ConfirmData): Promise<ConfirmResult> {
    const ref = this.dialog.open<ConfirmDialog, ConfirmData, ConfirmResult>(ConfirmDialog, { data, maxWidth: '520px', autoFocus: 'dialog' });
    return (await firstValueFrom(ref.afterClosed())) ?? false;
  }
}
