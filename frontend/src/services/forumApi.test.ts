import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get, post, put, remove } = vi.hoisted(() => ({
  get: vi.fn().mockResolvedValue({ data: {} }),
  post: vi.fn().mockResolvedValue({ data: {} }),
  put: vi.fn().mockResolvedValue({ data: {} }),
  remove: vi.fn().mockResolvedValue({ data: {} })
}));

vi.mock('./api', () => ({ api: { get, post, put, delete: remove } }));

import { forumApi } from './forumApi';

describe('forumApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('sends search and paging as query parameters', () => {
    forumApi.list({ search: 'ars', page: 2, pageSize: 10 });

    expect(get).toHaveBeenCalledWith('/api/forum', {
      params: { search: 'ars', page: 2, pageSize: 10 }
    });
  });

  it('uses the vote replacement and withdrawal routes', () => {
    forumApi.voteComment('comment-1', -1);
    forumApi.removeCommentVote('comment-1');

    expect(put).toHaveBeenCalledWith('/api/forum/comments/comment-1/vote', { value: -1 });
    expect(remove).toHaveBeenCalledWith('/api/forum/comments/comment-1/vote');
  });

  it('forwards a parent comment when creating a reply', () => {
    forumApi.createComment('post-1', { tekst: 'Odgovor', parentCommentId: 'comment-1' });

    expect(post).toHaveBeenCalledWith('/api/forum/post-1/comments', {
      tekst: 'Odgovor',
      parentCommentId: 'comment-1'
    });
  });
});
