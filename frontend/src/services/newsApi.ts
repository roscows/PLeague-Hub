import type {
  CommentRequest,
  CreateNewsArticleRequest,
  CreateXNewsRequest,
  ForumComment,
  NewsDetail,
  NewsListQuery,
  NewsSource,
  NewsSourceRequest,
  NewsSourceSyncResponse,
  NewsTimelineResponse,
  UpdateNewsRequest
} from '../types/api';
import { api } from './api';

function withoutUndefined<T extends object>(value: T) {
  return Object.fromEntries(Object.entries(value).filter(([, item]) => item !== undefined));
}

export const newsApi = {
  async list(query: NewsListQuery = {}) {
    const params = withoutUndefined(query);
    const response = Object.keys(params).length
      ? await api.get<NewsTimelineResponse>('/api/news', { params })
      : await api.get<NewsTimelineResponse>('/api/news');
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<NewsDetail>(`/api/news/${id}`);
    return response.data;
  },
  async create(request: CreateNewsArticleRequest) {
    const response = await api.post<NewsDetail>('/api/news', request);
    return response.data;
  },
  async createX(request: CreateXNewsRequest) {
    const response = await api.post<NewsDetail>('/api/news/x', request);
    return response.data;
  },
  async update(id: string, request: UpdateNewsRequest) {
    const response = await api.put<NewsDetail>(`/api/news/${id}`, request);
    return response.data;
  },
  async remove(id: string) {
    await api.delete(`/api/news/${id}`);
  },
  async listComments(id: string) {
    const response = await api.get<ForumComment[]>(`/api/news/${id}/comments`);
    return response.data;
  },
  async createComment(id: string, request: CommentRequest) {
    const response = await api.post<ForumComment>(`/api/news/${id}/comments`, request);
    return response.data;
  },
  async listSources() {
    const response = await api.get<NewsSource[]>('/api/news/sources');
    return response.data;
  },
  async createSource(request: NewsSourceRequest) {
    const response = await api.post<NewsSource>('/api/news/sources', request);
    return response.data;
  },
  async updateSource(id: string, request: NewsSourceRequest) {
    const response = await api.put<NewsSource>(`/api/news/sources/${id}`, request);
    return response.data;
  },
  async deactivateSource(id: string) {
    await api.delete(`/api/news/sources/${id}`);
  },
  async pauseSource(id: string, razlog: string) {
    const response = await api.put<NewsSource>(`/api/news/sources/${id}/pause`, { razlog });
    return response.data;
  },
  async resumeSource(id: string) {
    const response = await api.delete<NewsSource>(`/api/news/sources/${id}/pause`);
    return response.data;
  },
  async syncSource(id: string) {
    const response = await api.post<NewsSourceSyncResponse>(`/api/news/sources/${id}/sync`);
    return response.data;
  }
};
