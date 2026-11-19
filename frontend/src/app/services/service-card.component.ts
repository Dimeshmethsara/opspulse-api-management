import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { HistoryBucket, MonitoredService } from '../core/api.models';
import { StatusBadgeComponent } from '../shared/status-badge.component';

@Component({
  selector: 'app-service-card',
  standalone: true,
  imports: [StatusBadgeComponent, DecimalPipe, DatePipe],
  template: `
    <article class="service-card">
      <div class="card-top"><span class="service-icon" aria-hidden="true">{{ service().name.slice(0, 2).toUpperCase() }}</span><app-status-badge [status]="service().status" /></div>
      <h3>{{ service().name }}</h3>
      <p class="service-description">{{ service().description }}</p>
      <div class="card-stats"><div><span>Availability · 24h</span><strong>{{ service().availabilityPercent === null ? '—' : (service().availabilityPercent | number:'1.2-2') + '%' }}</strong></div><div><span>Response time</span><strong>{{ service().responseTimeMs === null ? '—' : service().responseTimeMs + ' ms' }}</strong></div></div>
      <div class="history-bars" role="img" [attr.aria-label]="service().name + ': hourly status for the last 24 hours. See availability history below for details.'">
        @for (bucket of service().history; track bucket.from) {
          <span [class]="'history-bar ' + (bucket.status?.toLowerCase() ?? 'unknown')" [title]="bucketTitle(bucket)"></span>
        }
      </div>
      <div class="card-foot"><span>24 hours ago</span><span>{{ service().isStale ? 'Check overdue' : 'Latest: ' + (service().lastCheckedAt | date:'HH:mm:ss') }}</span></div>
    </article>
  `,
})
export class ServiceCardComponent {
  readonly service = input.required<MonitoredService>();
  bucketTitle(bucket: HistoryBucket): string {
    return `${new Date(bucket.from).toLocaleString()}: ${bucket.status ?? 'No checks'} · ${bucket.samples} samples`;
  }
}
