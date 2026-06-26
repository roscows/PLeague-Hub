import type { Season, StandingRow } from '../types/api';
import { api } from './api';

export const standingsApi = {
  async getSeasons() {
    const response = await api.get<Season[]>('/api/standings/seasons');
    return response.data;
  },
  async getStandings(season: string) {
    const response = await api.get<StandingRow[]>('/api/standings', {
      params: { season }
    });
    return response.data;
  }
};
