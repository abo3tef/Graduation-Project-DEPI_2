import type {SessionUserRole} from '../session/session-store';

/**
 * Where each role belongs after signing in (F6-C2). Admins have no management screens until F11
 * (D-C2-1), so they land on the public home page with a signed-in notice.
 */
export function landingPathFor(role: SessionUserRole | null): string {
  switch (role) {
    case 'Doctor':
      return '/dashboard';
    case 'Secretary':
      return '/appointments';
    default:
      return '/';
  }
}

/**
 * Which signed-in areas a role may open. This is UX only — the API enforces access itself
 * (AGENTS.md §5). Keeping it here means the login redirect and the route guard cannot disagree.
 */
const ALLOWED_PREFIXES: Readonly<Record<SessionUserRole, readonly string[]>> = {
  Doctor: ['/dashboard', '/appointments', '/medical-records'],
  Secretary: ['/appointments'],
  Admin: []
};

export function isPathAllowedForRole(role: SessionUserRole, path: string): boolean {
  // Only same-origin paths, and never a protocol-relative URL (`//host`).
  if (!path.startsWith('/') || path.startsWith('//')) {
    return false;
  }

  const clean = (path.split('?')[0] ?? '').split('#')[0] ?? '';

  return ALLOWED_PREFIXES[role].some(
    (prefix) => clean === prefix || clean.startsWith(`${prefix}/`)
  );
}

/** Honours `?next=` only when the role may open it, otherwise sends the user to its landing page. */
export function resolveRedirectTarget(
  role: SessionUserRole | null,
  next: string | null | undefined
): string {
  if (role === null) {
    return '/login';
  }

  const target = next?.trim();

  return target && isPathAllowedForRole(role, target)
    ? target
    : landingPathFor(role);
}
