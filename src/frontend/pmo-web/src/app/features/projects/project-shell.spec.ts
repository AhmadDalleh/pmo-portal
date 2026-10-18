import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { AuthService } from '../../core/auth/auth.service';
import { Project } from '../../core/models/project.models';
import { ProjectShell } from './project-shell';

/**
 * Regression test for the endless GET /api/projects/{id} loop: the effect used to track the `project` signal that
 * ProjectContext.load() reads and then writes, so every response triggered another request.
 */
describe('ProjectShell', () => {
  const project = { id: 7, code: 'PRJ-007', name: 'Test project', status: 'active', departmentName: 'D', ownerName: 'O', startDate: '2026-01-01', plannedEndDate: '2026-12-31' } as unknown as Project;
  let get: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<ProjectShell>;

  /** Several change-detection rounds, so a re-triggering effect would have time to fire again. */
  const settle = async () => {
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await fixture.whenStable();
    }
  };

  beforeEach(() => {
    get = vi.fn((id: number) => of({ ...project, id }));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ProjectsApi, useValue: { get } },
        { provide: AuthService, useValue: { canManage: () => true } },
      ],
    });
    fixture = TestBed.createComponent(ProjectShell);
  });

  it('loads the project once and does not request it again after the response', async () => {
    fixture.componentRef.setInput('id', '7');
    await settle();

    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith(7);
  });

  it('requests the project exactly once more when the route id changes', async () => {
    fixture.componentRef.setInput('id', '7');
    await settle();

    fixture.componentRef.setInput('id', '8');
    await settle();

    expect(get).toHaveBeenCalledTimes(2);
    expect(get).toHaveBeenLastCalledWith(8);
  });
});
