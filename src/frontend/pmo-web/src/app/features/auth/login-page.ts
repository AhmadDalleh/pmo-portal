import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ApiError } from '../../core/models/common.models';
import { toApiError } from '../../core/http/api-error';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    <div class="wrap">
      <form class="panel" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="login-title">
        <h1 id="login-title"><mat-icon aria-hidden="true">insights</mat-icon> PMO Portal</h1>
        <p class="muted">Sign in with your work email.</p>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="username" required />
          @if (form.controls.email.touched && form.controls.email.invalid) { <mat-error>Enter your email address.</mat-error> }
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Password</mat-label>
          <input matInput type="password" formControlName="password" autocomplete="current-password" required />
          @if (form.controls.password.touched && form.controls.password.invalid) { <mat-error>Enter your password.</mat-error> }
        </mat-form-field>
        @if (error()) { <p class="error" role="alert"><mat-icon aria-hidden="true">error_outline</mat-icon> {{ error()?.detail }}</p> }
        @if (saving()) { <mat-progress-bar mode="indeterminate" aria-label="Signing in" /> }
        <button mat-flat-button type="submit" class="full" [disabled]="saving()">Sign in</button>
      </form>
    </div>
  `,
  styles: `
    .wrap { min-height: 100vh; display: grid; place-items: center; padding: 16px; box-sizing: border-box; }
    form { width: 100%; max-width: 380px; display: flex; flex-direction: column; }
    h1 { display: flex; align-items: center; gap: 8px; }
    .error { color: var(--mat-sys-error); display: flex; gap: 6px; align-items: center; margin: 0 0 12px; }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected async submit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving.set(true);
    this.error.set(null);
    try {
      await this.auth.login(this.form.controls.email.value, this.form.controls.password.value);
      await this.router.navigateByUrl(this.route.snapshot.queryParamMap.get('returnUrl') ?? '/projects');
    } catch (e) {
      const err = toApiError(e);
      // The server answers the same way for every failure, so the page does too.
      this.error.set(err.status === 401 ? { ...err, detail: 'Invalid email or password.' } : err);
    } finally {
      this.saving.set(false);
    }
  }
}
