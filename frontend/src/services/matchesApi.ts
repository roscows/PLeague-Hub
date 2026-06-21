import type { Match, MatchCreateRequest, MatchFilters, MatchUpdateRequest } from '../types/api';
import { api } from './api';

export const matchesApi = {
  async list(filters: MatchFilters = {}) {
    const response = await api.get<Match[]>('/api/matches', { params: filters });
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<Match>(`/api/matches/${id}`);
    return response.data;
  },
  async create(request: MatchCreateRequest) {
    const response = await api.post<Match>('/api/matches', request);
    return response.data;
  },
  async update(id: string, request: MatchUpdateRequest) {
    const response = await api.put<Match>(`/api/matches/${id}`, request);
    return response.data;
  },
  async remove(id: string) {
    await api.delete(`/api/matches/${id}`);
  }
};
