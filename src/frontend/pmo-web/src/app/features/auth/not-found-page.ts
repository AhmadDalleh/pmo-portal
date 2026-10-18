import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink],
  template: `
    <div class="page">
      <h1>Page not found</h1>
      <p class="muted">The page does not exist, or you may not see it. <a routerLink="/projects">Go to projects</a>.</p>
    </div>
  `,
})
export class NotFoundPage {}
