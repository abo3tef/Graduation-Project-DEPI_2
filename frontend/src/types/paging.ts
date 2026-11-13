/**
 * Shared paged-list contract (STATE.md Q-04, `backend/docs/api/CONVENTIONS.md`).
 *
 * The request carries `pageNumber`; the response carries `page`. Features must
 * not redefine these limits or this envelope.
 */
export const DEFAULT_PAGE_SIZE = 10;
export const MAX_PAGE_SIZE = 50;

export type PagedResult<T> = {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
};
