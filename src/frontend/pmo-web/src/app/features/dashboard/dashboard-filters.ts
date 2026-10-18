import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { ProjectsApi } from '../../core/api/projects.api';
import { DashboardFilter } from '../../core/models/dashboard.models';
import { LookupItem, UserLookup } from '../../core/models/common.models';
import { PROJECT_STATUSES, ProjectListItem } from '../../core/models/project.models';

/** The filter bar (story G3). It only reports what the user picked; the page writes it into the URL and every widget refetches. */
@Component({
  selector: 'app-dashboard-filters',
  imports: [FormsModule, MatFormFieldModule, MatSelectModule, MatInputModule, MatButtonModule, MatTooltipModule],
  template: `
    <form class="row bar" aria-label="Dashboard filters">
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Department</mat-label>
        <mat-select [ngModel]="filter().departmentId" name="department" (ngModelChange)="patch({ departmentId: $event })">
          <mat-option [value]="undefined">All</mat-option>@for (d of departments(); track d.id) { <mat-option [value]="d.id">{{ d.name }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Project</mat-label>
        <mat-select [ngModel]="filter().projectId" name="project" (ngModelChange)="patch({ projectId: $event })">
          <mat-option [value]="undefined">All</mat-option>@for (p of projects(); track p.id) { <mat-option [value]="p.id">{{ p.code }} {{ p.name }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Project manager</mat-label>
        <mat-select [ngModel]="filter().managerId" name="manager" (ngModelChange)="patch({ managerId: $event })">
          <mat-option [value]="undefined">All</mat-option>@for (m of managers(); track m.id) { <mat-option [value]="m.id">{{ m.fullName }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select [ngModel]="filter().status" name="status" (ngModelChange)="patch({ status: $event })">
          <mat-option [value]="undefined">All</mat-option>@for (s of statuses; track s) { <mat-option [value]="s">{{ s.replace('_', ' ') }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic" matTooltip="Keeps projects whose dates overlap this period">
        <mat-label>Period from</mat-label><input matInput type="date" name="from" [ngModel]="filter().from" (ngModelChange)="patch({ from: $event })" />
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Period to</mat-label><input matInput type="date" name="to" [ngModel]="filter().to" (ngModelChange)="patch({ to: $event })" />
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic" matTooltip="Figures are calculated as of this date (default: the reporting date)">
        <mat-label>As of</mat-label><input matInput type="date" name="asOf" [ngModel]="filter().asOf" (ngModelChange)="patch({ asOf: $event })" />
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic" matTooltip="Applies to the monthly trend, spend by category and the cost list only">
        <mat-label>Cost category (finance charts)</mat-label>
        <mat-select [ngModel]="filter().categoryId" name="category" (ngModelChange)="patch({ categoryId: $event })">
          <mat-option [value]="undefined">All</mat-option>@for (c of categories(); track c.id) { <mat-option [value]="c.id">{{ c.name }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <button mat-button type="button" (click)="clear.emit()">Clear filters</button>
    </form>
  `,
  styles: `.bar { gap: 8px; margin-bottom: 12px; } mat-form-field { width: 170px; }`,
})
export class DashboardFilters {
  readonly filter = input.required<DashboardFilter>();
  readonly changed = output<Partial<DashboardFilter>>();
  readonly clear = output<void>();

  private readonly projectsApi = inject(ProjectsApi);
  private readonly financeApi = inject(FinanceApi);

  protected readonly statuses = PROJECT_STATUSES;
  protected readonly departments = signal<LookupItem[]>([]);
  protected readonly projects = signal<ProjectListItem[]>([]);
  protected readonly managers = signal<UserLookup[]>([]);
  protected readonly categories = signal<LookupItem[]>([]);

  constructor() {
    void firstValueFrom(this.projectsApi.departments()).then((d) => this.departments.set(d));
    void firstValueFrom(this.projectsApi.list({ sort: 'code', dir: 'asc', page: 1, pageSize: 100 })).then((p) => this.projects.set(p.items));
    void firstValueFrom(this.projectsApi.users('ProjectManager')).then((u) => this.managers.set(u));
    void firstValueFrom(this.financeApi.categories()).then((c) => this.categories.set(c));
  }

  protected patch(p: Partial<DashboardFilter>): void { this.changed.emit(p); }
}
