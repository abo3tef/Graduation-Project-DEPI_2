export type AuthHeaders = Readonly<Record<string, string>>;

export type AuthHeadersProvider = () => AuthHeaders;

const providers = new Set<AuthHeadersProvider>();

/**
 * The ONE place where "who is calling" is attached to API requests.
 *
 * Each provider contributes a small header map and is consulted on every request,
 * so a changed identity is picked up without re-registering. Contributions are
 * merged in registration order (a later provider wins on a shared header name).
 * Returns an unregister function.
 *
 * Today two development mechanisms contribute here: the temporary
 * `X-User-Id` / `X-User-Role` headers (appointments) and the development
 * `Authorization: Bearer …` token (medical records). When real authentication is
 * delivered (STATE.md Q-07), register a provider that returns the production
 * credential instead; no screen or hook changes.
 */
export function registerAuthHeadersProvider(provider: AuthHeadersProvider): () => void {
  providers.add(provider);

  return () => {
    providers.delete(provider);
  };
}

/** Test helper: forget every registered provider. */
export function resetAuthHeadersProviders(): void {
  providers.clear();
}

export function getAuthHeaders(): AuthHeaders {
  const merged: Record<string, string> = {};

  for (const provider of providers) {
    Object.assign(merged, provider());
  }

  return merged;
}
