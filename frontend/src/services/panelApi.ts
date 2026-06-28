import type { ModerationActivity, PanelUser, StaffNotice } from '../types/api';
import { api } from './api';

export const panelApi = {
  async listNotices() {
    return (await api.get<StaffNotice[]>('/api/moderation/notices')).data;
  },
  async createNotice(tekst: string) {
    return (await api.post<StaffNotice>('/api/moderation/notices', { tekst })).data;
  },
  async removeNotice(id: string) {
    await api.delete(`/api/moderation/notices/${id}`);
  },
  async pinNotice(id: string) {
    await api.post(`/api/moderation/notices/${id}/pin`);
  },
  async unpinNotice(id: string) {
    await api.delete(`/api/moderation/notices/${id}/pin`);
  },
  async listActivity(limit = 20) {
    return (await api.get<ModerationActivity[]>('/api/moderation/activity', { params: { limit } })).data;
  },
  async searchUsers(q: string, staffOnly = false) {
    return (await api.get<PanelUser[]>('/api/moderation/users', { params: { q, staffOnly } })).data;
  },
  async changeRole(id: string, uloga: 'registrovani' | 'moderator') {
    return (await api.put<PanelUser>(`/api/moderation/users/${id}/role`, { uloga })).data;
  }
};
