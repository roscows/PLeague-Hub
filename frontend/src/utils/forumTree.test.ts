import { describe, expect, it } from 'vitest';
import type { ForumComment } from '../types/api';
import { buildCommentTree } from './forumTree';

const baseComment: Omit<ForumComment, 'id' | 'parentCommentId' | 'broj'> = {
  postId: 'post-1',
  autorId: 'user-1',
  autorUsername: 'fan',
  autorUloga: 'registrovani',
  tekst: 'Komentar',
  datumKreiranja: '2026-06-21T10:00:00Z',
  obrisan: false,
  lajkovi: 0,
  dislajkovi: 0,
  trenutniGlas: null
};

describe('buildCommentTree', () => {
  it('orders siblings chronologically and links replies', () => {
    const tree = buildCommentTree([
      { ...baseComment, id: 'child', parentCommentId: 'root', broj: 2, datumKreiranja: '2026-06-21T10:05:00Z' },
      { ...baseComment, id: 'root', parentCommentId: null, broj: 1 },
      { ...baseComment, id: 'sibling', parentCommentId: null, broj: 3, datumKreiranja: '2026-06-21T10:10:00Z' }
    ]);

    expect(tree.map((comment) => comment.id)).toEqual(['root', 'sibling']);
    expect(tree[0].children[0].id).toBe('child');
    expect(tree[0].children[0].depth).toBe(2);
  });

  it('caps visual depth at three and keeps orphaned replies visible', () => {
    const tree = buildCommentTree([
      { ...baseComment, id: 'root', parentCommentId: null, broj: 1 },
      { ...baseComment, id: 'two', parentCommentId: 'root', broj: 2 },
      { ...baseComment, id: 'three', parentCommentId: 'two', broj: 3 },
      { ...baseComment, id: 'four', parentCommentId: 'three', broj: 4 },
      { ...baseComment, id: 'orphan', parentCommentId: 'missing', broj: 5 }
    ]);

    expect(tree[0].children[0].children[0].children[0].depth).toBe(3);
    expect(tree.some((comment) => comment.id === 'orphan')).toBe(true);
  });
});
