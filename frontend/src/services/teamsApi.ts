import type { Team, TeamWriteRequest } from '../types/api';
import { api } from './api';

export const teamsApi = {
  async list() {
    const response = await api.get<Team[]>('/api/teams');
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<Team>(`/api/teams/${id}`);
    return response.data;
  },
  async create(request: TeamWriteRequest) {
    const response = await api.post<Team>('/api/teams', request);
    return response.data;
  },
  async update(id: string, request: TeamWriteRequest) {
    const response = await api.put<Team>(`/api/teams/${id}`, request);
    return response.data;
  },
  async remove(id: string) {
    await api.delete(`/api/teams/${id}`);
  }
};
