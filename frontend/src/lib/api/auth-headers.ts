export type AuthHeaders = Readonly<Record<string, string>>;

export type AuthHeadersProvider = () => AuthHeaders;

const NO_HEADERS: AuthHeaders = {};

let provider: AuthHeadersProvider = () => NO_HEADERS;

/**
 * The ONE place where "who is calling" is attached to API requests.
 *
 * Today the development identity registers a provider that returns the
 * temporary `X-User-Id` / `X-User-Role` headers. When real authentication is
 * delivered (STATE.md Q-07), register a provider that returns
 * `Authorization: Bearer …` instead (or switch the client to cookies); no
 * screen or hook changes.
 */
export function setAuthHeadersProvider(next: AuthHeadersProvider): void {
  provider = next;
}

export function resetAuthHeadersProvider(): void {
  provider = () => NO_HEADERS;
}

export function getAuthHeaders(): AuthHeaders {
  return provider();
}
