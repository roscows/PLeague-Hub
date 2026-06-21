import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ForumComment, ForumDiscussion } from '../types/api';

const { getById, listComments, createComment, voteComment, removeCommentVote, useAuth } = vi.hoisted(() => ({
  getById: vi.fn(),
  listComments: vi.fn(),
  createComment: vi.fn(),
  voteComment: vi.fn(),
  removeCommentVote: vi.fn(),
  useAuth: vi.fn()
}));

vi.mock('../services/forumApi', () => ({
  forumApi: { getById, listComments, createComment, voteComment, removeCommentVote }
}));
vi.mock('../contexts/AuthContext', () => ({ useAuth }));

import { ForumDiscussionPage } from './ForumDiscussion';

const discussion: ForumDiscussion = {
  id: 'topic-1',
  naslov: 'Ko osvaja Premier League?',
  sadrzaj: 'Argumentujte ko je najveci favorit.',
  autorId: 'author-1',
  autorUsername: 'admin',
  autorUloga: 'administrator',
  datumKreiranja: '2026-06-21T10:00:00Z',
  istaknut: false
};

const comments: ForumComment[] = [
  {
    id: 'comment-1', postId: 'topic-1', parentCommentId: null, autorId: 'user-1', autorUsername: 'fan',
    autorUloga: 'registrovani', tekst: 'Arsenal ima kontinuitet.', datumKreiranja: '2026-06-21T10:10:00Z',
    obrisan: false, broj: 1, lajkovi: 2, dislajkovi: 0, trenutniGlas: null
  },
  {
    id: 'comment-2', postId: 'topic-1', parentCommentId: 'comment-1', autorId: 'user-2', autorUsername: 'moderator',
    autorUloga: 'moderator', tekst: 'City ima dublji sastav.', datumKreiranja: '2026-06-21T10:20:00Z',
    obrisan: false, broj: 2, lajkovi: 1, dislajkovi: 1, trenutniGlas: null
  },
  {
    id: 'comment-3', postId: 'topic-1', parentCommentId: 'comment-2', autorId: 'user-3', autorUsername: 'oldfan',
    autorUloga: 'registrovani', tekst: 'Komentar je uklonjen', datumKreiranja: '2026-06-21T10:30:00Z',
    obrisan: true, broj: 3, lajkovi: 0, dislajkovi: 0, trenutniGlas: null
  }
];

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/forum/topic-1']}>
      <Routes>
        <Route path="/forum/:id" element={<ForumDiscussionPage />} />
        <Route path="/login" element={<div>Prijava</div>} />
      </Routes>
    </MemoryRouter>
  );
}

describe('Forum discussion page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getById.mockResolvedValue(discussion);
    listComments.mockResolvedValue(comments);
    createComment.mockResolvedValue({ ...comments[1], id: 'comment-4', broj: 4, tekst: 'Novi odgovor' });
    voteComment.mockResolvedValue({ commentId: 'comment-2', lajkovi: 2, dislajkovi: 1, trenutniGlas: 1 });
    removeCommentVote.mockResolvedValue({ commentId: 'comment-2', lajkovi: 1, dislajkovi: 1, trenutniGlas: null });
    useAuth.mockReturnValue({ isAuthenticated: true, user: { userId: 'user-1' } });
  });

  it('renders the original post and numbered nested comments', async () => {
    renderPage();

    expect(await screen.findByRole('heading', { name: discussion.naslov })).toBeInTheDocument();
    expect(screen.getByText('Argumentujte ko je najveci favorit.')).toBeInTheDocument();
    expect(screen.getByText('#1')).toBeInTheDocument();
    expect(screen.getByText('#2')).toBeInTheDocument();
    expect(screen.getByText('#3')).toBeInTheDocument();
    expect(screen.getByText('Komentar je uklonjen')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Svidja mi se komentaru #1' })).toBeDisabled();
  });

  it('sends a single vote and updates its selected state', async () => {
    const user = userEvent.setup();
    renderPage();

    const like = await screen.findByRole('button', { name: 'Svidja mi se komentaru #2' });
    await user.click(like);

    await waitFor(() => expect(voteComment).toHaveBeenCalledWith('comment-2', 1));
    expect(like).toHaveAttribute('aria-pressed', 'true');
  });

  it('redirects a guest vote to login', async () => {
    useAuth.mockReturnValue({ isAuthenticated: false, user: null });
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'Svidja mi se komentaru #2' }));

    expect(await screen.findByText('Prijava')).toBeInTheDocument();
    expect(voteComment).not.toHaveBeenCalled();
  });

  it('creates an inline reply for the selected comment', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole('button', { name: 'Odgovori na komentar #2' }));
    await user.type(screen.getByLabelText('Tekst odgovora'), 'Novi odgovor');
    await user.click(screen.getByRole('button', { name: 'Posalji odgovor' }));

    await waitFor(() => expect(createComment).toHaveBeenCalledWith('topic-1', {
      tekst: 'Novi odgovor',
      parentCommentId: 'comment-2'
    }));
  });
});
