import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, exhaustMap, finalize, merge, Subject, timer } from 'rxjs';
import { OpsPulseApi } from './core/opspulse-api.service';
import { ServiceStatus, StatusSummary } from './core/api.models';
import { ServiceCardComponent } from './services/service-card.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [DatePipe, DecimalPipe, ServiceCardComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  private readonly api = inject(OpsPulseApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshRequests = new Subject<void>();
  readonly data = signal<StatusSummary | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly search = signal('');
  readonly serviceFilter = signal('All');
  readonly incidentFilter = signal<'All' | 'Open' | 'Resolved'>('All');
  readonly statusFilters = ['All', 'Healthy', 'Degraded', 'Down'];
  readonly incidentFilters = ['All', 'Open', 'Resolved'] as const;
  readonly healthyCount = computed(() => this.data()?.services.filter(service => service.status === 'Healthy').length ?? 0);
  readonly affectedCount = computed(() => this.data()?.services.filter(service => service.status !== 'Healthy').length ?? 0);
  readonly fleetAvailability = computed(() => {
    const values = this.data()?.services.flatMap(service => service.availabilityPercent === null ? [] : [service.availabilityPercent]) ?? [];
    return values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : null;
  });
  readonly filteredServices = computed(() => this.data()?.services.filter(service =>
    (this.serviceFilter() === 'All' || service.status === this.serviceFilter()) &&
    `${service.name} ${service.description}`.toLowerCase().includes(this.search().toLowerCase().trim())) ?? []);
  readonly filteredIncidents = computed(() => this.data()?.recentIncidents.filter(incident =>
    this.incidentFilter() === 'All' || incident.status === this.incidentFilter()) ?? []);
  readonly headline = computed(() => {
    if (this.error()) return 'Waiting for a fresh status update.';
    if (this.data()?.services.some(service => service.isStale)) return 'Some health checks are overdue.';
    const status = this.data()?.overallStatus;
    return status === 'Healthy' ? 'All systems are running smoothly.' : status === 'Degraded' ? 'Some services are running slowly.' : 'A few services need attention.';
  });

  constructor() {
    merge(timer(0, 30_000), this.refreshRequests).pipe(
      exhaustMap(() => {
        this.loading.set(true);
        return this.api.getStatus().pipe(
          catchError(() => { this.error.set(true); return EMPTY; }),
          finalize(() => this.loading.set(false)),
        );
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(summary => { this.data.set(summary); this.error.set(false); });
  }

  refresh(): void { this.refreshRequests.next(); }
  setSearch(event: Event): void { this.search.set((event.target as HTMLInputElement).value); }
  setServiceFilter(event: Event): void { this.serviceFilter.set((event.target as HTMLSelectElement).value); }
  historyLabel(from: string, status: ServiceStatus | null, samples: number): string {
    return `${new Date(from).toLocaleString()}: ${status ?? 'No checks'}, ${samples} samples`;
  }
}
