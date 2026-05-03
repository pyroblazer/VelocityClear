import { getJson } from './apiClient';

export interface ServiceHealth {
  name: string;
  status: string;
  url: string;
  responseTime?: number;
}

export interface GatewayHealthResponse {
  status: string;
  services: Record<string, ServiceHealth>;
}

export interface GatewayStatusResponse {
  activeSseConnections: number;
  eventBusBackend: string;
}

export interface AuditStatsResponse {
  totalEvents: number;
  eventsByType: Record<string, number>;
  earliestLog?: string;
  latestLog?: string;
}

export const gatewayApi = {
  getHealth: () => getJson<GatewayHealthResponse>('/api/gateway/health'),
  getStatus: () => getJson<GatewayStatusResponse>('/api/gateway/status'),
};

export const auditApi = {
  getStats: () => getJson<AuditStatsResponse>('/api/audit/stats'),
  list: (page = 1, pageSize = 50) => getJson<unknown[]>(`/api/audit?page=${page}&pageSize=${pageSize}`),
  verify: () => getJson<{ isValid: boolean; totalLogs: number; verifiedLogs: number }>('/api/audit/verify'),
};
