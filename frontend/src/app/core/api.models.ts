export type ServiceStatus = 'Healthy' | 'Degraded' | 'Down';
export interface HistoryBucket {
  from: string;
  status: ServiceStatus | null;
  samples: number;
  availabilityPercent: number | null;
}
export interface MonitoredService {
  id: string;
  name: string;
  description: string;
  status: ServiceStatus;
  lastCheckedAt: string | null;
  responseTimeMs: number | null;
  isStale: boolean;
  availabilityPercent: number | null;
  sampleCount: number;
  history: HistoryBucket[];
}
export interface Incident {
  id: string;
  serviceId: string;
  serviceName: string;
  title: string;
  description: string;
  severity: 'Minor' | 'Major' | 'Critical';
  status: 'Open' | 'Resolved';
  createdAt: string;
  resolvedAt: string | null;
  resolution: string | null;
}
export interface StatusSummary {
  generatedAt: string;
  isDemo: boolean;
  overallStatus: ServiceStatus;
  pollIntervalSeconds: number;
  openIncidentCount: number;
  services: MonitoredService[];
  recentIncidents: Incident[];
}
