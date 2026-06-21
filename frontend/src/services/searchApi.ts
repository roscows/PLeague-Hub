import type { SearchResult } from '../types/api';
import { api } from './api';

export const searchApi = {
  async search(query: string, limit = 8) {
    const response = await api.get<SearchResult[]>('/api/search', {
      params: { q: query, limit }
    });
    return response.data;
  }
};
