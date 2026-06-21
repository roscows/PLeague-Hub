import { describe, expect, it } from 'vitest';
import { resolveApiAssetUrl } from './assets';

describe('resolveApiAssetUrl', () => {
  it('resolves API-relative team logo paths against the backend URL', () => {
    expect(resolveApiAssetUrl('/team-logos/60.png')).toBe(
      'http://localhost:5000/team-logos/60.png'
    );
  });

  it('preserves absolute and data URLs', () => {
    expect(resolveApiAssetUrl('https://example.com/logo.png')).toBe(
      'https://example.com/logo.png'
    );
    expect(resolveApiAssetUrl('data:image/png;base64,AAAA')).toBe(
      'data:image/png;base64,AAAA'
    );
  });

  it('returns an empty string for a missing source', () => {
    expect(resolveApiAssetUrl('')).toBe('');
    expect(resolveApiAssetUrl()).toBe('');
  });
});
