'use client';

import {useCallback, useSyncExternalStore} from 'react';

/**
 * Development-only token bridge (D-28).
 *
 * The token lives in `sessionStorage` (one tab, cleared when the tab closes) and is exposed
 * through a tiny external store so every mounted screen sees the same value. It is never
 * logged, never rendered and never sent anywhere except the Medilog API.
 */
const STORAGE_KEY = 'medilog.dev-token';

const listeners = new Set<() => void>();

let cachedToken: string | null | undefined;

function readStoredToken(): string | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    const value = window.sessionStorage.getItem(STORAGE_KEY);
    return value && value.trim().length > 0 ? value.trim() : null;
  } catch {
    return null;
  }
}

function getSnapshot(): string | null {
  if (cachedToken === undefined) {
    cachedToken = readStoredToken();
  }

  return cachedToken;
}

function getServerSnapshot(): string | null {
  // There is no token during SSR; the client snapshot takes over after hydration.
  return null;
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}

function emit(): void {
  for (const listener of listeners) {
    listener();
  }
}

function subscribeToNothing(): () => void {
  return () => {};
}

function getClientReadySnapshot(): boolean {
  return true;
}

function getServerReadySnapshot(): boolean {
  return false;
}

/** Non-reactive read for mutation handlers that run outside React render. */
export function readDevToken(): string | null {
  return getSnapshot();
}

export type DevTokenState = {
  token: string | null;
  hasToken: boolean;
  /** False during SSR/hydration so the UI can show a neutral state instead of flashing. */
  isReady: boolean;
  saveToken: (value: string) => boolean;
  clearToken: () => void;
};

export function useDevToken(): DevTokenState {
  const token = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
  // `false` during SSR/hydration, `true` afterwards — without a setState-in-effect cascade.
  const isReady = useSyncExternalStore(
    subscribeToNothing,
    getClientReadySnapshot,
    getServerReadySnapshot
  );

  const saveToken = useCallback((value: string): boolean => {
    const trimmed = value.trim();

    try {
      if (trimmed.length === 0) {
        window.sessionStorage.removeItem(STORAGE_KEY);
        cachedToken = null;
      } else {
        window.sessionStorage.setItem(STORAGE_KEY, trimmed);
        cachedToken = trimmed;
      }

      emit();
      return true;
    } catch {
      // Some browsers block storage (private mode, disabled cookies). The caller reports it.
      return false;
    }
  }, []);

  const clearToken = useCallback((): void => {
    try {
      window.sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      // Ignore: the in-memory value below is still cleared.
    }

    cachedToken = null;
    emit();
  }, []);

  return {token, hasToken: token !== null, isReady, saveToken, clearToken};
}
