import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/http/api-error';
import { ApiError, LoadStatus } from '../../core/models/common.models';
import { Project } from '../../core/models/project.models';

/**
 * The project the tabs are showing. Provided by ProjectShell, so every tab (tasks, finance, risks...) reads the same
 * project id, version and "can I manage it" flag. After any change a tab calls reload() instead of patching values.
 */
@Injectable()
export class ProjectContext {
  private readonly api = inject(ProjectsApi);
  private readonly auth = inject(AuthService);

  readonly project = signal<Project | null>(null);
  readonly status = signal<LoadStatus>('idle');
  readonly error = signal<ApiError | null>(null);
  readonly id = computed(() => this.project()?.id ?? 0);
  /** Cosmetic: shows or hides buttons. The server still refuses what the caller may not do. */
  readonly canManage = computed(() => this.auth.canManage(this.id()));

  async load(id: number): Promise<void> {
    if (!this.project() || this.project()!.id !== id) {
      this.status.set('loading');   // a background reload keeps the tabs on screen
    }

    try {
      this.project.set(await firstValueFrom(this.api.get(id)));
      this.status.set('idle');
    } catch (e) {
      this.error.set(toApiError(e));
      this.status.set('error');
    }
  }

  reload(): Promise<void> {
    return this.load(this.id());
  }
}
