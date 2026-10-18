import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { FinanceApi } from '../../core/api/finance.api';
import { ProjectCosts } from './project-costs';

/**
 * A tab must fetch once when it opens and once per user action. The effect tracks only projectId, and the handlers
 * (page change) call load() themselves, so a page change must not be followed by a second request from the effect.
 */
describe('ProjectCosts', () => {
  let costs: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<ProjectCosts>;

  const settle = async () => {
    for (let i = 0; i < 5; i++) {
      fixture.detectChanges();
      await fixture.whenStable();
    }
  };

  beforeEach(() => {
    costs = vi.fn(() => of({ items: [], totalCount: 0, page: 1, pageSize: 20 }));
    TestBed.configureTestingModule({ providers: [{ provide: FinanceApi, useValue: { costs, categories: () => of([]) } }] });
    fixture = TestBed.createComponent(ProjectCosts);
    fixture.componentRef.setInput('projectId', 7);
  });

  it('loads the costs once when the tab opens', async () => {
    await settle();

    expect(costs).toHaveBeenCalledTimes(1);
    expect(costs.mock.calls[0][0]).toBe(7);
  });

  it('sends exactly one more request when the page changes', async () => {
    await settle();

    (fixture.componentInstance as unknown as { onPage(p: { page: number; pageSize: number }): void }).onPage({ page: 2, pageSize: 20 });
    await settle();

    expect(costs).toHaveBeenCalledTimes(2);
    expect(costs.mock.calls[1][1]).toMatchObject({ page: 2 });
  });
});
