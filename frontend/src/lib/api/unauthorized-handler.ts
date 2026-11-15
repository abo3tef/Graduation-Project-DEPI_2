export type UnauthorizedHandler = () => void;

const handlers = new Set<UnauthorizedHandler>();

/**
 * One place reacts when the API answers `401`, so screens never have to clear credentials or
 * navigate themselves. `lib/api` stays free of feature imports: the auth feature registers the
 * handler, mirroring the auth-headers registry.
 *
 * Returns an unregister function.
 */
export function registerUnauthorizedHandler(handler: UnauthorizedHandler): () => void {
  handlers.add(handler);

  return () => {
    handlers.delete(handler);
  };
}

/** Test helper: forget every registered handler. */
export function resetUnauthorizedHandlers(): void {
  handlers.clear();
}

/**
 * Notifies the handlers that a request was rejected with `401`. A handler must never break the
 * request pipeline, so a throwing handler is swallowed here; the `ApiError` is still thrown by
 * the caller.
 */
export function notifyUnauthorized(): void {
  for (const handler of handlers) {
    try {
      handler();
    } catch {
      // A failing side effect must not hide the real 401 from the caller.
    }
  }
}
