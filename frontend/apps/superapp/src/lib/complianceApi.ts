export interface KycProfileResponse {
  id: string;
  userId: string;
  status: string;
  fullName: string;
  livenessChecked: boolean;
  livenessConfidence: number;
  watchlistScreened: boolean;
  watchlistHit: boolean;
}

export interface ConsentRecordResponse {
  id: string;
  consentType: string;
  status: string;
  grantedAt: string;
}

export interface AmlAlertResponse {
  id: string;
  ruleTriggered: string;
  severity: string;
  status: string;
}

export interface ApprovalResponse {
  id: string;
  approvalType: string;
  requestedBy: string;
  status: string;
}

export interface ComplaintResponse {
  id: string;
  subject: string;
  category: string;
  status: string;
  slaBreach: boolean;
}

export interface SocDashboardResponse {
  openIncidents: number;
}

const BASE = 'http://localhost:5004';

async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

// KYC
export const kycApi = {
  initiate: (body: object) => fetchJson<KycProfileResponse>('/api/kyc/initiate', { method: 'POST', body: JSON.stringify(body) }),
  getByUser: (userId: string) => fetchJson<KycProfileResponse>(`/api/kyc/user/${userId}`),
  isVerified: (userId: string) => fetchJson<boolean>(`/api/kyc/user/${userId}/verified`),
  liveness: (kycProfileId: string, userId: string) =>
    fetchJson<KycProfileResponse>(`/api/kyc/${kycProfileId}/liveness`, { method: 'POST', body: JSON.stringify(userId) }),
  screen: (kycProfileId: string, body: object) =>
    fetchJson<KycProfileResponse>(`/api/kyc/${kycProfileId}/screen`, { method: 'POST', body: JSON.stringify(body) }),
};

// Consent
export const consentApi = {
  grant: (body: object) => fetchJson<ConsentRecordResponse>('/api/consent/grant', { method: 'POST', body: JSON.stringify(body) }),
  withdraw: (body: object) => fetchJson<ConsentRecordResponse>('/api/consent/withdraw', { method: 'POST', body: JSON.stringify(body) }),
  listByUser: (userId: string) => fetchJson<ConsentRecordResponse[]>(`/api/consent/user/${userId}`),
  check: (userId: string, type: string) => fetchJson<boolean>(`/api/consent/user/${userId}/check/${type}`),
};

// AML
export const amlApi = {
  listAlerts: (status?: string) => fetchJson<AmlAlertResponse[]>(`/api/aml/alerts${status ? `?status=${status}` : ''}`),
  resolveAlert: (alertId: string, body: object) =>
    fetchJson<AmlAlertResponse>(`/api/aml/alerts/${alertId}/resolve`, { method: 'POST', body: JSON.stringify(body) }),
  fileSar: (body: object) => fetchJson('/api/aml/sar', { method: 'POST', body: JSON.stringify(body) }),
  listSars: (status?: string) => fetchJson(`/api/aml/sar${status ? `?status=${status}` : ''}`),
};

// Approvals
export const approvalApi = {
  list: (status?: string) => fetchJson<ApprovalResponse[]>(`/api/approvals${status ? `?status=${status}` : ''}`),
  create: (body: object) => fetchJson<ApprovalResponse>('/api/approvals', { method: 'POST', body: JSON.stringify(body) }),
  process: (id: string, body: object) =>
    fetchJson<ApprovalResponse>(`/api/approvals/${id}/process`, { method: 'POST', body: JSON.stringify(body) }),
};

// Reports
export const reportApi = {
  list: () => fetchJson('/api/reports'),
  generate: (body: object) => fetchJson('/api/reports', { method: 'POST', body: JSON.stringify(body) }),
  download: (reportId: string) => `${BASE}/api/reports/${reportId}/download`,
};

// Complaints
export const complaintApi = {
  list: (userId?: string) => fetchJson<ComplaintResponse[]>(`/api/complaints${userId ? `?userId=${userId}` : ''}`),
  create: (body: object) => fetchJson<ComplaintResponse>('/api/complaints', { method: 'POST', body: JSON.stringify(body) }),
  acknowledge: (id: string) => fetchJson<ComplaintResponse>(`/api/complaints/${id}/acknowledge`, { method: 'POST' }),
  resolve: (id: string, body: object) =>
    fetchJson<ComplaintResponse>(`/api/complaints/${id}/resolve`, { method: 'POST', body: JSON.stringify(body) }),
  escalate: (id: string, body: object) =>
    fetchJson<ComplaintResponse>(`/api/complaints/${id}/escalate`, { method: 'POST', body: JSON.stringify(body) }),
};

// SOC
export const socApi = {
  dashboard: () => fetchJson<SocDashboardResponse>('/api/soc/dashboard'),
  listIncidents: (status?: string) => fetchJson(`/api/soc/incidents${status ? `?status=${status}` : ''}`),
  createIncident: (body: object) => fetchJson('/api/soc/incidents', { method: 'POST', body: JSON.stringify(body) }),
  updateIncident: (id: string, body: object) =>
    fetchJson(`/api/soc/incidents/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
};

// Infrastructure Compliance
export const infraApi = {
  drpStatus: () => fetchJson('/api/infrastructure-compliance/drp'),
  dataResidency: () => fetchJson('/api/infrastructure-compliance/data-residency'),
  vendors: () => fetchJson('/api/infrastructure-compliance/vendors'),
};
