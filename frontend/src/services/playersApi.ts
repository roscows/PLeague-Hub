import type { Player, PlayerFilters, PlayerProfile, PlayerWriteRequest } from '../types/api';
import { api } from './api';

export const playersApi = {
  async getProfile(providerId: number) {
    const response = await api.get<PlayerProfile>(`/api/players/${providerId}`);
    return response.data;
  },
  async list(filters: PlayerFilters = {}) {
    const response = await api.get<Player[]>('/api/players', { params: filters });
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<Player>(`/api/players/${id}`);
    return response.data;
  },
  async create(request: PlayerWriteRequest) {
    const response = await api.post<Player>('/api/players', request);
    return response.data;
  },
  async update(id: string, request: PlayerWriteRequest) {
    const response = await api.put<Player>(`/api/players/${id}`, request);
    return response.data;
  },
  async remove(id: string) {
    await api.delete(`/api/players/${id}`);
  }
};
