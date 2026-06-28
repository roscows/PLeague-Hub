import type { CommentReport, CreateReportRequest } from '../types/api';
import { api } from './api';

export const reportsApi = {
  async create(commentId: string, body: CreateReportRequest) {
    await api.post(`/api/forum/comments/${commentId}/report`, body);
  },
  async listPending() {
    const response = await api.get<CommentReport[]>('/api/moderation/reports');
    return response.data;
  },
  async resolve(id: string, akcija: 'obrisi' | 'odbaci') {
    await api.post(`/api/moderation/reports/${id}/resolve`, { akcija });
  }
};
