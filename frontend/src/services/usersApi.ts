import type { UserProfile } from '../types/api';
import { api } from './api';

export const usersApi = {
  async getMe() {
    const response = await api.get<UserProfile>('/api/users/me');
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<UserProfile>(`/api/users/${id}`);
    return response.data;
  },
  async updateFavoriteTeams(teamIds: string[]) {
    const response = await api.put<UserProfile>('/api/users/me/favorite-teams', { teamIds });
    return response.data;
  }
};
