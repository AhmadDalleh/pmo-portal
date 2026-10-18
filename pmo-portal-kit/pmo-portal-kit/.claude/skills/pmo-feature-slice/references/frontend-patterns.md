# Frontend patterns (Angular standalone + signals, Angular Material, ngx-echarts)

## Contents
1. Folder shape
2. API client
3. Feature store with refetch-after-mutation
4. Async view states
5. Forms: server errors onto fields
6. Confirm consequential actions
7. Dashboard filters in the URL
8. Chart drill-down
9. HTTP interceptors

---

## 1. Folder shape
```
src/app/
  core/        auth/ (auth.service, auth.guard, role.guard), api/ (projects.api.ts, finance.api.ts ...),
               http/ (auth.interceptor, correlation.interceptor, error.interceptor), models/
  shared/      data-table/, confirm-dialog/, kpi-card/, chart-card/, status-badge/, async-state/
  features/    projects/, tasks/, risks/, change-requests/, finance/, dashboard/, admin/, notifications/
  app.routes.ts  (lazy-loaded feature routes, guarded by role where useful — UX only)
```

## 2. API client
```ts
@Injectable({ providedIn: 'root' })
export class FinanceApi {
  private http = inject(HttpClient);
  summary(projectId: number, asOf: string) {
    return this.http.get<FinanceSummary>(`/api/projects/${projectId}/finance/summary`, { params: { asOf } });
  }
  createDraft(projectId: number, body: CreateCost, idempotencyKey: string) {
    return this.http.post<Cost>(`/api/projects/${projectId}/costs`, body,
      { headers: { 'Idempotency-Key': idempotencyKey } });
  }
  post(costId: number, version: number) {
    return this.http.post<Cost>(`/api/finance/costs/${costId}/post`, { version });
  }
}
```
One file per backend resource; typed models mirror API DTOs; no business math here.

## 3. Feature store with refetch-after-mutation
```ts
@Injectable()
export class ProjectFinanceStore {
  private api = inject(FinanceApi);
  readonly summary = signal<FinanceSummary | null>(null);
  readonly status = signal<'idle' | 'loading' | 'error'>('idle');
  readonly error = signal<ApiError | null>(null);

  async load(projectId: number, asOf: string) {
    this.status.set('loading');
    try { this.summary.set(await firstValueFrom(this.api.summary(projectId, asOf))); this.status.set('idle'); }
    catch (e) { this.error.set(toApiError(e)); this.status.set('error'); }
  }

  async postCost(projectId: number, costId: number, version: number, asOf: string) {
    await firstValueFrom(this.api.post(costId, version));
    await this.load(projectId, asOf);     // recompute comes from SQL — never patch numbers here
  }
}
```

## 4. Async view states
`<app-async-state [status]="store.status()" [empty]="!rows().length" [error]="store.error()" (retry)="reload()">` wraps every list, card and chart: skeleton while loading, "No data for these filters" when empty, message + correlation id + Retry on error.

## 5. Forms: server errors onto fields
```ts
catch (e) {
  const err = toApiError(e);                       // reads ProblemDetails
  if (err.status === 400 && err.errors)
    for (const [field, msgs] of Object.entries(err.errors))
      this.form.get(camel(field))?.setErrors({ server: msgs[0] });
  else this.snack.open(err.detail ?? 'Request failed', 'Close');
}
```
Client validators mirror server rules (required, amount > 0, end ≥ start) for fast feedback; the server is still the authority.
Generate the Idempotency-Key with `crypto.randomUUID()` when the form opens, not on submit. Disable submit while saving.

## 6. Confirm consequential actions
Post cost, reverse cost, decide CR, delete draft, change role → `ConfirmDialog` stating exactly what will happen ("Post AED 12,500.00 to PRJ-002? Posted costs cannot be edited."). On 409 show the server `detail` and reload the record.

## 7. Dashboard filters in the URL
```ts
readonly filters = toSignal(inject(ActivatedRoute).queryParamMap.pipe(map(toDashboardFilter)), { requireSync: true });
constructor() {
  effect(() => { const f = this.filters(); untracked(() => this.store.loadAll(f)); });  // every widget refetches
}
setFilter(patch: Partial<DashboardFilter>) {
  this.router.navigate([], { queryParams: patch, queryParamsHandling: 'merge' });
}
```
Shareable links, back button works, reload keeps state. `categoryId` is passed only to finance endpoints.

## 8. Chart drill-down
```ts
onBarClick(e: ECElementEvent) {
  const projectId = this.byProject()[e.dataIndex].projectId;
  this.router.navigate(['/dashboard/drilldown/costs'], {
    queryParams: { ...this.filters(), projectId }, // same filters → same WHERE on the server
  });
}
```
Drill-down pages show the row count and the sum, and the KPI it should match.

## 9. HTTP interceptors
- `correlation.interceptor`: adds `X-Correlation-Id` (uuid) to every request; error state displays it.
- `auth.interceptor`: attaches the in-memory access token; on 401 calls `/api/auth/refresh` once (cookie), retries, else logs out.
- `error.interceptor`: normalizes errors into `ApiError { status, code, detail, errors, correlationId }`.
Access token is kept in memory (signal), not localStorage.
