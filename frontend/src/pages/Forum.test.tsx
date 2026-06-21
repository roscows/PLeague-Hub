import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { PagedResponse, ForumTopic } from '../types/api';

const { list, create, useAuth } = vi.hoisted(() => ({
  list: vi.fn(),
  create: vi.fn(),
  useAuth: vi.fn()
}));

vi.mock('../services/forumApi', () => ({ forumApi: { list, create } }));
vi.mock('../contexts/AuthContext', () => ({ useAuth }));

import { Forum } from './Forum';

const page: PagedResponse<ForumTopic> = {
  items: [
    {
      id: 'topic-1',
      naslov: 'Ko osvaja Premier League?',
      autorId: 'user-1',
      autorUsername: 'fan',
      brojOdgovora: 12,
      datumKreiranja: '2026-06-21T10:00:00Z',
      poslednjaAktivnost: '2026-06-21T11:55:00Z',
      poslednjiAutorUsername: 'admin',
      istaknut: false
    }
  ],
  page: 1,
  pageSize: 20,
  total: 1,
  totalPages: 1
};

describe('Forum topic list', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    list.mockResolvedValue(page);
    create.mockResolvedValue({ id: 'topic-2' });
    useAuth.mockReturnValue({ isAuthenticated: false });
  });

  it('renders the Serbian topic table and discussion link', async () => {
    render(<MemoryRouter><Forum /></MemoryRouter>);

    expect(await screen.findByText('Ko osvaja Premier League?')).toBeInTheDocument();
    expect(screen.getByText('Tema')).toBeInTheDocument();
    expect(screen.getByText('Odgovori')).toBeInTheDocument();
    expect(screen.getByText('Autor')).toBeInTheDocument();
    expect(screen.getByText('Aktivnost')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ko osvaja Premier League?' })).toHaveAttribute('href', '/forum/topic-1');
    expect(screen.getByRole('link', { name: 'Nova tema' })).toHaveAttribute('href', '/login');
  });

  it('debounces search and resets the requested page', async () => {
    render(<MemoryRouter><Forum /></MemoryRouter>);

    const search = await screen.findByPlaceholderText('Pretrazi teme');
    fireEvent.change(search, { target: { value: 'arsenal' } });

    await waitFor(() => expect(list).toHaveBeenLastCalledWith({ search: 'arsenal', page: 1, pageSize: 20 }), {
      timeout: 1000
    });
  });

  it('allows an authenticated user to create a topic', async () => {
    useAuth.mockReturnValue({ isAuthenticated: true });
    const user = userEvent.setup();
    render(<MemoryRouter><Forum /></MemoryRouter>);

    await user.click(await screen.findByRole('button', { name: 'Nova tema' }));
    await user.type(screen.getByLabelText('Naslov teme'), 'Nova tema');
    await user.type(screen.getByLabelText('Sadrzaj teme'), 'Ovo je sadrzaj nove teme.');
    await user.click(screen.getByRole('button', { name: 'Objavi temu' }));

    await waitFor(() => expect(create).toHaveBeenCalledWith({
      naslov: 'Nova tema',
      sadrzaj: 'Ovo je sadrzaj nove teme.'
    }));
  });
});
