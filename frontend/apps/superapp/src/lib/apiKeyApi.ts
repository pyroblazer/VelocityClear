import { getJson, postJson, delJson } from './apiClient';

export interface ApiKeyResponse {
  id: string;
  name: string;
  keyPrefix: string;
  permissions: string[];
  createdAt: string;
  lastUsedAt: string | null;
  isActive: boolean;
}

export interface CreateApiKeyResponse extends ApiKeyResponse {
  apiKey: string;
}

export const apiKeyApi = {
  list: () => getJson<ApiKeyResponse[]>('/api/apikeys'),
  create: (name: string, permissions: string[]) =>
    postJson<CreateApiKeyResponse>('/api/apikeys', { name, permissions }),
  revoke: (id: string) => delJson(`/api/apikeys/${id}`),
};
