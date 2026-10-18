import { Routes } from '@angular/router';
import { adminGuard, authGuard, managerGuard } from './core/auth/auth.guard';

/** Feature pages are loaded on demand. Guards only improve navigation; every API call is checked again by the server. */
export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login-page').then((m) => m.LoginPage) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'projects' },
      { path: 'projects', loadComponent: () => import('./features/projects/projects-page').then((m) => m.ProjectsPage) },
      { path: 'projects/new', canActivate: [managerGuard], loadComponent: () => import('./features/projects/project-form-page').then((m) => m.ProjectFormPage) },
      {
        path: 'projects/:id',
        loadComponent: () => import('./features/projects/project-shell').then((m) => m.ProjectShell),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'overview' },
          { path: 'overview', loadComponent: () => import('./features/projects/project-overview').then((m) => m.ProjectOverview) },
          { path: 'tasks', loadComponent: () => import('./features/tasks/project-tasks').then((m) => m.ProjectTasks) },
          { path: 'milestones', loadComponent: () => import('./features/tasks/project-milestones').then((m) => m.ProjectMilestones) },
          { path: 'finance', loadComponent: () => import('./features/finance/project-finance').then((m) => m.ProjectFinance) },
          { path: 'risks', loadComponent: () => import('./features/risks/project-risks').then((m) => m.ProjectRisks) },
          { path: 'change-requests', loadComponent: () => import('./features/change-requests/project-change-requests').then((m) => m.ProjectChangeRequests) },
          { path: 'members', loadComponent: () => import('./features/projects/project-members').then((m) => m.ProjectMembers) },
        ],
      },
      { path: 'my-work', loadComponent: () => import('./features/tasks/my-work-page').then((m) => m.MyWorkPage) },
      { path: 'approvals', canActivate: [managerGuard], loadComponent: () => import('./features/change-requests/approvals-page').then((m) => m.ApprovalsPage) },
      { path: 'change-requests/:id', loadComponent: () => import('./features/change-requests/change-request-page').then((m) => m.ChangeRequestPage) },
      { path: 'notifications', loadComponent: () => import('./features/notifications/notifications-page').then((m) => m.NotificationsPage) },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin-shell').then((m) => m.AdminShell),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'users' },
          { path: 'users', loadComponent: () => import('./features/admin/admin-users').then((m) => m.AdminUsers) },
          { path: 'audit', loadComponent: () => import('./features/admin/admin-audit').then((m) => m.AdminAudit) },
          { path: 'automation', loadComponent: () => import('./features/admin/admin-automation').then((m) => m.AdminAutomation) },
          { path: 'categories', loadComponent: () => import('./features/admin/admin-categories').then((m) => m.AdminCategories) },
        ],
      },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage) },
      { path: 'dashboard/drilldown/:kind', loadComponent: () => import('./features/dashboard/drilldown-page').then((m) => m.DrilldownPage) },
    ],
  },
  { path: '**', loadComponent: () => import('./features/auth/not-found-page').then((m) => m.NotFoundPage) },
];
