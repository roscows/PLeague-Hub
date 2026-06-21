import { describe, expect, it } from 'vitest';
import { hasRequiredRole } from './authorization';

describe('hasRequiredRole', () => {
  it('allows a user whose role is in the required role list', () => {
    expect(hasRequiredRole('moderator', ['moderator', 'administrator'])).toBe(true);
  });

  it('rejects a user whose role is not in the required role list', () => {
    expect(hasRequiredRole('registrovani', ['administrator'])).toBe(false);
  });
});
