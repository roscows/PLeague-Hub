import type { HealthResponse } from '../types/api';
import { api } from './api';

export const healthApi = {
  async get() {
    const response = await api.get<HealthResponse>('/api/health');
    return response.data;
  }
};
