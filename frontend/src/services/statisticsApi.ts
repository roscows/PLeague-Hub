import type { Statistic, StatisticFilters, StatisticWriteRequest } from '../types/api';
import { api } from './api';

export const statisticsApi = {
  async list(filters: StatisticFilters = {}) {
    const response = await api.get<Statistic[]>('/api/statistics', { params: filters });
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<Statistic>(`/api/statistics/${id}`);
    return response.data;
  },
  async create(request: StatisticWriteRequest) {
    const response = await api.post<Statistic>('/api/statistics', request);
    return response.data;
  },
  async update(id: string, request: StatisticWriteRequest) {
    const response = await api.put<Statistic>(`/api/statistics/${id}`, request);
    return response.data;
  },
  async remove(id: string) {
    await api.delete(`/api/statistics/${id}`);
  }
};
