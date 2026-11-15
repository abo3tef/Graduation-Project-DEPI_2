import {act, renderHook} from '@testing-library/react';
import {afterEach, describe, expect, it} from 'vitest';
import {useDevToken} from './use-dev-token';

describe('useDevToken credential revision', () => {
  afterEach(() => {
    window.sessionStorage.clear();
  });

  it('increments only when the stored credential changes without exposing it in the revision', () => {
    const {result} = renderHook(() => useDevToken());
    const initialRevision = result.current.tokenRevision;

    act(() => {
      expect(result.current.saveToken('credential-a')).toBe(true);
    });
    expect(result.current.tokenRevision).toBe(initialRevision + 1);

    act(() => {
      expect(result.current.saveToken('credential-a')).toBe(true);
    });
    expect(result.current.tokenRevision).toBe(initialRevision + 1);

    act(() => {
      expect(result.current.saveToken('credential-b')).toBe(true);
    });
    expect(result.current.tokenRevision).toBe(initialRevision + 2);

    act(() => {
      result.current.clearToken();
    });
    expect(result.current.tokenRevision).toBe(initialRevision + 3);
    expect(result.current.token).toBeNull();
  });
});
