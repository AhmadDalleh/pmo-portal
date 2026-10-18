export type Role = 'Administrator' | 'ProjectManager' | 'Contributor';

export interface Me {
  id: number;
  email: string;
  fullName: string;
  role: Role;
  departmentId: number;
  /** Projects this user may manage. Only used to show or hide buttons: the server checks every action again. */
  managedProjectIds: number[];
}

export interface LoginResponse {
  accessToken: string;
  expiresInSeconds: number;
  user: Me;
}

export interface NotificationItem {
  id: number;
  title: string;
  body: string;
  linkUrl: string | null;
  createdAt: string;
  readAt: string | null;
}
