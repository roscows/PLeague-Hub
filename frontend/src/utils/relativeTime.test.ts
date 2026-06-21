import { describe, expect, it } from 'vitest';
import { formatRelativeTime } from './relativeTime';

const now = new Date('2026-06-21T12:00:00Z');

describe('formatRelativeTime', () => {
  it('formats recent activity in Serbian Latin', () => {
    expect(formatRelativeTime('2026-06-21T11:59:45Z', now)).toBe('pre nekoliko sekundi');
    expect(formatRelativeTime('2026-06-21T11:55:00Z', now)).toBe('pre 5 minuta');
    expect(formatRelativeTime('2026-06-21T10:00:00Z', now)).toBe('pre 2 sata');
  });

  it('uses juce for the previous calendar day', () => {
    expect(formatRelativeTime('2026-06-20T18:00:00Z', now)).toBe('juce');
  });
});
