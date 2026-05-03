import { getJson, postJson, putJson } from './apiClient';

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

// KYC
export const kycApi = {
  initiate: (body: object) => postJson<KycProfileResponse>('/api/kyc/initiate', body),
  getByUser: (userId: string) => getJson<KycProfileResponse>(`/api/kyc/user/${userId}`),
  isVerified: (userId: string) => getJson<boolean>(`/api/kyc/user/${userId}/verified`),
  liveness: (kycProfileId: string, userId: string) =>
    postJson<KycProfileResponse>(`/api/kyc/${kycProfileId}/liveness`, userId),
  screen: (kycProfileId: string, body: object) =>
    postJson<KycProfileResponse>(`/api/kyc/${kycProfileId}/screen`, body),
};

// Consent
export const consentApi = {
  grant: (body: object) => postJson<ConsentRecordResponse>('/api/consent/grant', body),
  withdraw: (body: object) => postJson<ConsentRecordResponse>('/api/consent/withdraw', body),
  listByUser: (userId: string) => getJson<ConsentRecordResponse[]>(`/api/consent/user/${userId}`),
  check: (userId: string, type: string) => getJson<boolean>(`/api/consent/user/${userId}/check/${type}`),
};

// AML
export const amlApi = {
  listAlerts: (status?: string) => getJson<AmlAlertResponse[]>(`/api/aml/alerts${status ? `?status=${status}` : ''}`),
  resolveAlert: (alertId: string, body: object) =>
    postJson<AmlAlertResponse>(`/api/aml/alerts/${alertId}/resolve`, body),
  fileSar: (body: object) => postJson('/api/aml/sar', body),
  listSars: (status?: string) => getJson(`/api/aml/sar${status ? `?status=${status}` : ''}`),
};

// Approvals
export const approvalApi = {
  list: (status?: string) => getJson<ApprovalResponse[]>(`/api/approvals${status ? `?status=${status}` : ''}`),
  create: (body: object) => postJson<ApprovalResponse>('/api/approvals', body),
  process: (id: string, body: object) =>
    postJson<ApprovalResponse>(`/api/approvals/${id}/process`, body),
};

// Reports
export const reportApi = {
  list: () => getJson('/api/reports'),
  generate: (body: object) => postJson('/api/reports', body),
  download: (reportId: string) => `/api/reports/${reportId}/download`,
};

// Complaints
export const complaintApi = {
  list: (userId?: string) => getJson<ComplaintResponse[]>(`/api/complaints${userId ? `?userId=${userId}` : ''}`),
  create: (body: object) => postJson<ComplaintResponse>('/api/complaints', body),
  acknowledge: (id: string) => postJson<ComplaintResponse>(`/api/complaints/${id}/acknowledge`),
  resolve: (id: string, body: object) =>
    postJson<ComplaintResponse>(`/api/complaints/${id}/resolve`, body),
  escalate: (id: string, body: object) =>
    postJson<ComplaintResponse>(`/api/complaints/${id}/escalate`, body),
};

// SOC
export const socApi = {
  dashboard: () => getJson<SocDashboardResponse>('/api/soc/dashboard'),
  listIncidents: (status?: string) => getJson(`/api/soc/incidents${status ? `?status=${status}` : ''}`),
  createIncident: (body: object) => postJson('/api/soc/incidents', body),
  updateIncident: (id: string, body: object) =>
    putJson(`/api/soc/incidents/${id}`, body),
};

// Infrastructure Compliance
export const infraApi = {
  drpStatus: () => getJson('/api/infrastructure-compliance/drp'),
  dataResidency: () => getJson('/api/infrastructure-compliance/data-residency'),
  vendors: () => getJson('/api/infrastructure-compliance/vendors'),
};
