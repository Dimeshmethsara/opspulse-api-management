import { Component, input } from '@angular/core';
import { ServiceStatus } from '../core/api.models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="status-badge" [class]="'status-badge ' + status().toLowerCase()"><span class="status-dot" aria-hidden="true"></span>{{ status() }}</span>`,
})
export class StatusBadgeComponent { readonly status = input.required<ServiceStatus>(); }
