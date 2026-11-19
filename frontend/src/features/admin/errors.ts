import {isApiError} from '@/lib/api';

/**
 * Admin errors are translated by the stable API code, never by the English server message.
 * The returned key is relative to `Admin.notifications`.
 */
export function getAdminErrorKey(error: unknown): string {
  if (!isApiError(error)) {
    return 'error';
  }

  switch (error.code) {
    case 'admin.self_deactivation_forbidden':
      return 'selfDeactivationForbidden';
    case 'admin.last_admin_protected':
      return 'lastAdminProtected';
    case 'admin.role_change_blocked':
      return 'roleChangeBlocked';
    case 'admin.role_change_forbidden':
      return 'roleChangeForbidden';
    case 'admin.self_role_change_forbidden':
      return 'selfRoleChangeForbidden';
    default:
      return 'error';
  }
}

export function isRetryableAdminError(error: unknown): boolean {
  if (!isApiError(error)) {
    return true;
  }

  return !['validation', 'unauthorized', 'forbidden', 'not-found', 'conflict'].includes(
    error.kind
  );
}
