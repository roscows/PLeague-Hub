import type { ModerationActionRequest, ModerationState } from '../types/api';
import { api } from './api';

export const moderationApi = {
  async applyUserAction(id: string, request: ModerationActionRequest) {
    const response = await api.post<ModerationState>(`/api/moderation/users/${id}/actions`, request);
    return response.data;
  },
  async revokeUserAction(id: string) {
    await api.delete(`/api/moderation/users/${id}/action`);
  },
  async removePost(id: string) {
    await api.delete(`/api/moderation/posts/${id}`);
  },
  async removeComment(id: string) {
    await api.delete(`/api/moderation/comments/${id}`);
  },
  async pinPost(id: string) {
    await api.put(`/api/moderation/posts/${id}/pin`);
  },
  async unpinPost(id: string) {
    await api.delete(`/api/moderation/posts/${id}/pin`);
  },
  async pinComment(id: string) {
    await api.put(`/api/moderation/comments/${id}/pin`);
  },
  async unpinComment(id: string) {
    await api.delete(`/api/moderation/comments/${id}/pin`);
  }
};
