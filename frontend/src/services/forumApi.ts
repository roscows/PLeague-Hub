import type { Comment, CommentRequest, DiscussionRequest, Post } from '../types/api';
import { api } from './api';

export const forumApi = {
  async list() {
    const response = await api.get<Post[]>('/api/forum');
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<Post>(`/api/forum/${id}`);
    return response.data;
  },
  async create(request: DiscussionRequest) {
    const response = await api.post<Post>('/api/forum', request);
    return response.data;
  },
  async listComments(id: string) {
    const response = await api.get<Comment[]>(`/api/forum/${id}/comments`);
    return response.data;
  },
  async createComment(id: string, request: CommentRequest) {
    const response = await api.post<Comment>(`/api/forum/${id}/comments`, request);
    return response.data;
  }
};
