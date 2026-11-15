// Opaque, tab-local cache generation. Advanced by credential events, never by React rendering.
// No token, token fragment, or token-derived representation is exposed in query keys.
let scope = 0;

export function readCredentialScope(): number {
  return scope;
}

export function advanceCredentialScope(): void {
  scope += 1;
}
