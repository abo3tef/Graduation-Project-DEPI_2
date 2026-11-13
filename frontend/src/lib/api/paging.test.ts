import {describe, expect, it} from 'vitest';
import {createInvalidResponseError, isApiError} from './errors';
import {readPagedResult} from './paging';

const PAGE = {
  items: [{id: 'a'}, {id: 'b'}],
  page: 2,
  pageSize: 10,
  totalCount: 12,
  totalPages: 2
};

function readItem(value: unknown): {id: string} {
  if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
    const record = value as Record<string, unknown>;

    if (typeof record.id === 'string') {
      return {id: record.id};
    }
  }

  throw createInvalidResponseError(200);
}

function invalid(action: () => unknown) {
  try {
    action();
  } catch (error) {
    expect(isApiError(error) && error.kind === 'invalid-response').toBe(true);
    return;
  }

  throw new Error('expected an invalid-response error');
}

describe('readPagedResult', () => {
  it('reads the shared envelope and maps every item', () => {
    expect(readPagedResult(PAGE, readItem)).toEqual({
      items: [{id: 'a'}, {id: 'b'}],
      page: 2,
      pageSize: 10,
      totalCount: 12,
      totalPages: 2
    });
  });

  it('accepts an empty page', () => {
    expect(
      readPagedResult({items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0}, readItem)
    ).toMatchObject({items: [], page: 1, totalPages: 0});
  });

  it('ignores unknown members', () => {
    expect(readPagedResult({...PAGE, pageNumber: 99}, readItem)).toMatchObject({page: 2});
  });

  it('rejects a malformed envelope', () => {
    invalid(() => readPagedResult('x', readItem));
    invalid(() => readPagedResult([], readItem));
    invalid(() => readPagedResult({...PAGE, items: 'x'}, readItem));
    invalid(() => readPagedResult({...PAGE, page: '2'}, readItem));
    invalid(() => readPagedResult({...PAGE, pageSize: 10.5}, readItem));
    invalid(() => readPagedResult({...PAGE, totalCount: undefined}, readItem));
    invalid(() => readPagedResult({...PAGE, totalPages: null}, readItem));
  });

  it('propagates an item-reader failure', () => {
    invalid(() => readPagedResult({...PAGE, items: [{noId: true}]}, readItem));
  });
});
