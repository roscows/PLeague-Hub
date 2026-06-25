import type { MatchDetail } from '../types/api';
import { api } from './api';

export const matchDetailApi = {
  async get(matchId: string) {
    const response = await api.get<MatchDetail>(`/api/matches/${matchId}/detail`);
    return response.data;
  }
};
