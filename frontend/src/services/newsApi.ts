import type { DiscussionRequest, Post } from '../types/api';
import { api } from './api';

export const newsApi = {
  async list() {
    const response = await api.get<Post[]>('/api/news');
    return response.data;
  },
  async create(request: DiscussionRequest) {
    const response = await api.post<Post>('/api/news', request);
    return response.data;
  },
  async remove(id: string) {
    await api.delete(`/api/news/${id}`);
  }
};
