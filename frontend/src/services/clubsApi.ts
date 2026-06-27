import type { ClubProfile } from '../types/api';
import { api } from './api';

export const clubsApi = {
  async getProfile(providerId: number) {
    const response = await api.get<ClubProfile>(`/api/clubs/${providerId}`);
    return response.data;
  }
};
