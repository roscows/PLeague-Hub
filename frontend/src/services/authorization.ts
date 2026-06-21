import type { Role } from '../types/api';

export function hasRequiredRole(role: Role | undefined, requiredRoles: readonly Role[]) {
  return Boolean(role && requiredRoles.includes(role));
}
