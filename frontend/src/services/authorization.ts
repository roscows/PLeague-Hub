import type { Role } from '../types/api';

export function hasRequiredRole(role: Role | undefined, requiredRoles: readonly Role[]) {
  return Boolean(role && requiredRoles.includes(role));
}

export function canModerateRole(actor: Role | undefined, target: Role) {
  const rank: Record<Role, number> = {
    gost: 0,
    registrovani: 1,
    moderator: 2,
    administrator: 3
  };
  return Boolean(actor && target !== 'administrator' && rank[actor] > rank[target]);
}
