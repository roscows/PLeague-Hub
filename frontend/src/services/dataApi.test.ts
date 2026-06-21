import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get, post, put, remove } = vi.hoisted(() => ({
  get: vi.fn().mockResolvedValue({ data: [] }),
  post: vi.fn().mockResolvedValue({ data: {} }),
  put: vi.fn().mockResolvedValue({ data: {} }),
  remove: vi.fn().mockResolvedValue({ data: undefined })
}));

vi.mock('./api', () => ({
  api: { get, post, put, delete: remove }
}));

import { authApi } from './authApi';
import { forumApi } from './forumApi';
import { healthApi } from './healthApi';
import { matchesApi } from './matchesApi';
import { moderationApi } from './moderationApi';
import { newsApi } from './newsApi';
import { playersApi } from './playersApi';
import { searchApi } from './searchApi';
import { statisticsApi } from './statisticsApi';
import { teamsApi } from './teamsApi';
import { usersApi } from './usersApi';

describe('domain API clients', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses typed public collection endpoints and filter params', () => {
    teamsApi.list();
    playersApi.list({ search: 'saka', teamId: 'team-1' });
    matchesApi.list({ status: 'zavrsena', season: '2026/27' });
    statisticsApi.list({ matchId: 'match-1', playerId: 'player-1' });
    newsApi.list();
    healthApi.get();

    expect(get).toHaveBeenNthCalledWith(1, '/api/teams');
    expect(get).toHaveBeenNthCalledWith(2, '/api/players', { params: { search: 'saka', teamId: 'team-1' } });
    expect(get).toHaveBeenNthCalledWith(3, '/api/matches', { params: { status: 'zavrsena', season: '2026/27' } });
    expect(get).toHaveBeenNthCalledWith(4, '/api/statistics', { params: { matchId: 'match-1', playerId: 'player-1' } });
    expect(get).toHaveBeenNthCalledWith(5, '/api/news');
    expect(get).toHaveBeenNthCalledWith(6, '/api/health');
  });

  it('uses authentication and profile endpoints', () => {
    authApi.login({ emailOrUsername: 'admin', password: 'secret' });
    authApi.register({ username: 'marko', email: 'marko@example.com', password: 'secret' });
    usersApi.getMe();
    usersApi.updateFavoriteTeams(['team-1']);

    expect(post).toHaveBeenNthCalledWith(1, '/api/auth/login', { emailOrUsername: 'admin', password: 'secret' });
    expect(post).toHaveBeenNthCalledWith(2, '/api/auth/register', {
      username: 'marko',
      email: 'marko@example.com',
      password: 'secret'
    });
    expect(get).toHaveBeenCalledWith('/api/users/me');
    expect(put).toHaveBeenCalledWith('/api/users/me/favorite-teams', { teamIds: ['team-1'] });
  });

  it('uses forum topic and comment endpoints', () => {
    forumApi.list();
    forumApi.getById('post-1');
    forumApi.create({ naslov: 'Tema', sadrzaj: 'Sadrzaj' });
    forumApi.listComments('post-1');
    forumApi.createComment('post-1', { tekst: 'Komentar' });

    expect(get).toHaveBeenNthCalledWith(1, '/api/forum');
    expect(get).toHaveBeenNthCalledWith(2, '/api/forum/post-1');
    expect(post).toHaveBeenNthCalledWith(1, '/api/forum', { naslov: 'Tema', sadrzaj: 'Sadrzaj' });
    expect(get).toHaveBeenNthCalledWith(3, '/api/forum/post-1/comments');
    expect(post).toHaveBeenNthCalledWith(2, '/api/forum/post-1/comments', { tekst: 'Komentar' });
  });

  it('uses protected write and moderation endpoints', () => {
    const team = {
      naziv: 'Arsenal', skracenica: 'ARS', stadion: 'Emirates', osnovan: 1886,
      logoUrl: '/arsenal.png', bodovi: 18, pozicija: 1
    };

    teamsApi.create(team);
    teamsApi.update('team-1', team);
    teamsApi.remove('team-1');
    moderationApi.removePost('post-1');
    moderationApi.suspendUser('user-1');

    expect(post).toHaveBeenCalledWith('/api/teams', team);
    expect(put).toHaveBeenNthCalledWith(1, '/api/teams/team-1', team);
    expect(remove).toHaveBeenNthCalledWith(1, '/api/teams/team-1');
    expect(remove).toHaveBeenNthCalledWith(2, '/api/moderation/posts/post-1');
    expect(put).toHaveBeenNthCalledWith(2, '/api/moderation/users/user-1/suspend');
  });

  it('uses the global search endpoint with query and limit params', () => {
    searchApi.search('Erl', 8);

    expect(get).toHaveBeenCalledWith('/api/search', { params: { q: 'Erl', limit: 8 } });
  });
});
