const DEFAULT_API_BASE_URL = 'http://localhost:5208';

/**
 * Base URL of the Medilog Web API (D-27).
 *
 * `NEXT_PUBLIC_*` variables are inlined at build time, so this must stay a static
 * `process.env.NEXT_PUBLIC_API_BASE_URL` member access.
 */
function resolveApiBaseUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL?.trim();

  if (!configured) {
    return DEFAULT_API_BASE_URL;
  }

  return configured.replace(/\/+$/, '');
}

export const apiBaseUrl = resolveApiBaseUrl();
