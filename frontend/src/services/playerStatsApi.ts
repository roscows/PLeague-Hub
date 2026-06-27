import type { PlayerStat } from '../types/api';
import { api } from './api';

export const playerStatsApi = {
  async get(season: string) {
    const response = await api.get<PlayerStat[]>('/api/player-stats', {
      params: { season }
    });
    return response.data;
  }
};
