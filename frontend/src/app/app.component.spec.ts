import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { AppComponent } from './app.component';
import { OpsPulseApi } from './core/opspulse-api.service';
import { StatusSummary } from './core/api.models';

const summary: StatusSummary = {
  generatedAt: '2026-11-19T10:00:00Z', isDemo: true, overallStatus: 'Down', pollIntervalSeconds: 30, openIncidentCount: 1,
  services: [
    { id: 'identity', name: 'Identity', description: 'Access and sessions', status: 'Healthy', lastCheckedAt: '2026-11-19T10:00:00Z', responseTimeMs: 80, isStale: false, availabilityPercent: 100, sampleCount: 96, history: [] },
    { id: 'inventory', name: 'Inventory', description: 'Stock synchronization', status: 'Down', lastCheckedAt: '2026-11-19T10:00:00Z', responseTimeMs: null, isStale: false, availabilityPercent: 95, sampleCount: 96, history: [] },
  ],
  recentIncidents: [{ id: '1', serviceId: 'inventory', serviceName: 'Inventory', title: 'Synchronization unavailable', description: 'Stock updates are currently unavailable.', severity: 'Major', status: 'Open', createdAt: '2026-11-19T09:00:00Z', resolvedAt: null, resolution: null }],
};

describe('Dashboard', () => {
  let response: Subject<StatusSummary>;
  beforeEach(() => {
    response = new Subject<StatusSummary>();
    TestBed.configureTestingModule({ imports: [AppComponent], providers: [{ provide: OpsPulseApi, useValue: { getStatus: () => response } }] });
  });

  it('renders live status, service search and incident filtering', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    await new Promise(resolve => setTimeout(resolve, 10));
    response.next(summary);
    response.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('A few services need attention.');
    expect(fixture.nativeElement.querySelectorAll('app-service-card').length).toBe(2);
    fixture.componentInstance.search.set('stock');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('app-service-card').length).toBe(1);
    fixture.componentInstance.incidentFilter.set('Resolved');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No recent incidents in this view.');
    fixture.destroy();
  });

  it('shows a useful error when the API is unavailable', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    await new Promise(resolve => setTimeout(resolve, 10));
    response.error(new Error('Network unavailable'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Unable to reach the status API.');
    expect(fixture.nativeElement.querySelectorAll('app-service-card').length).toBe(0);
    fixture.destroy();
  });
});
