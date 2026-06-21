import type {
  CommentRequest,
  CommentVoteValue,
  DiscussionRequest,
  ForumComment,
  ForumDiscussion,
  ForumListQuery,
  ForumTopic,
  ForumVoteResponse,
  PagedResponse
} from '../types/api';
import { api } from './api';

export const forumApi = {
  async list(query?: ForumListQuery) {
    const response = query
      ? await api.get<PagedResponse<ForumTopic>>('/api/forum', { params: query })
      : await api.get<PagedResponse<ForumTopic>>('/api/forum');
    return response.data;
  },
  async getById(id: string) {
    const response = await api.get<ForumDiscussion>(`/api/forum/${id}`);
    return response.data;
  },
  async create(request: DiscussionRequest) {
    const response = await api.post<ForumDiscussion>('/api/forum', request);
    return response.data;
  },
  async listComments(id: string) {
    const response = await api.get<ForumComment[]>(`/api/forum/${id}/comments`);
    return response.data;
  },
  async createComment(id: string, request: CommentRequest) {
    const response = await api.post<ForumComment>(`/api/forum/${id}/comments`, request);
    return response.data;
  },
  async voteComment(commentId: string, value: CommentVoteValue) {
    const response = await api.put<ForumVoteResponse>(`/api/forum/comments/${commentId}/vote`, { value });
    return response.data;
  },
  async removeCommentVote(commentId: string) {
    const response = await api.delete<ForumVoteResponse>(`/api/forum/comments/${commentId}/vote`);
    return response.data;
  }
};
