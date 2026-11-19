import {apiRequest, readPagedResult} from '@/lib/api';
import type {PagedResult} from '@/types/paging';
import {readAdminUser, readSecretaryDoctors} from './readers';
import type {
  AdminRole,
  AdminStatus,
  AdminUser,
  AdminUserFilters,
  SecretaryDoctors
} from './types';

const BASE_PATH = '/api/admin';

/**
 * All admin calls use the single shared transport; the bearer token is attached centrally.
 * Every response is validated at this boundary before it reaches a screen.
 */
export async function listAdminUsers(
  filters: AdminUserFilters,
  signal?: AbortSignal
): Promise<PagedResult<AdminUser>> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/users`, {
    query: {
      role: filters.role === 'all' ? undefined : filters.role,
      status: filters.status === 'all' ? undefined : filters.status,
      search: filters.search.trim() || undefined,
      pageNumber: filters.pageNumber,
      pageSize: filters.pageSize
    },
    signal
  });

  return readPagedResult(data, readAdminUser);
}

export async function setAdminUserStatus(
  id: string,
  status: AdminStatus,
  signal?: AbortSignal
): Promise<AdminUser> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/users/${id}/status`, {
    method: 'PATCH',
    body: {status},
    signal
  });

  return readAdminUser(data);
}

export async function changeAdminUserRole(
  id: string,
  role: Extract<AdminRole, 'Doctor' | 'Secretary'>,
  signal?: AbortSignal
): Promise<AdminUser> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/users/${id}/role`, {
    method: 'PUT',
    body: {role},
    signal
  });

  return readAdminUser(data);
}

export async function getSecretaryDoctors(
  secretaryId: string,
  signal?: AbortSignal
): Promise<SecretaryDoctors> {
  const data = await apiRequest<unknown>(
    `${BASE_PATH}/secretaries/${secretaryId}/doctors`,
    {signal}
  );

  return readSecretaryDoctors(data);
}

export async function replaceSecretaryDoctors(
  secretaryId: string,
  doctorIds: readonly string[],
  signal?: AbortSignal
): Promise<SecretaryDoctors> {
  const data = await apiRequest<unknown>(
    `${BASE_PATH}/secretaries/${secretaryId}/doctors`,
    {
      method: 'PUT',
      body: {doctorIds: [...doctorIds]},
      signal
    }
  );

  return readSecretaryDoctors(data);
}
