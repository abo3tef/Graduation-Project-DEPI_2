import type {PagedResult} from '@/types/paging';
import {createInvalidResponseError} from './errors';

/**
 * Boundary reader for the shared paged-list envelope. The project has no schema
 * library, so this checks exactly the members the screens use and rejects
 * anything else as an invalid response.
 */
export function readPagedResult<T>(
  value: unknown,
  readItem: (item: unknown) => T
): PagedResult<T> {
  const source = record(value);

  if (!Array.isArray(source.items)) {
    fail();
  }

  return {
    items: (source.items as unknown[]).map(readItem),
    page: integer(source.page),
    pageSize: integer(source.pageSize),
    totalCount: integer(source.totalCount),
    totalPages: integer(source.totalPages)
  };
}

function fail(): never {
  throw createInvalidResponseError(200);
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : fail();
}

function integer(value: unknown): number {
  return typeof value === 'number' && Number.isInteger(value) ? value : fail();
}
