import { api } from './api';

export const moderationApi = {
  async removePost(id: string) {
    await api.delete(`/api/moderation/posts/${id}`);
  },
  async suspendUser(id: string) {
    await api.put(`/api/moderation/users/${id}/suspend`);
  }
};
