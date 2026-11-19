import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from './api-config';
import { Incident, MonitoredService, StatusSummary } from './api.models';

@Injectable({ providedIn: 'root' })
export class OpsPulseApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  getStatus() { return this.http.get<StatusSummary>(`${this.baseUrl}/api/status`); }
  getServices() { return this.http.get<MonitoredService[]>(`${this.baseUrl}/api/services`); }
  getIncidents() { return this.http.get<Incident[]>(`${this.baseUrl}/api/incidents?limit=100`); }
}
