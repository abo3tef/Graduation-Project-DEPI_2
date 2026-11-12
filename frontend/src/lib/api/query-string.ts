export type QueryValue = string | number | boolean | null | undefined;

/**
 * Builds a query string (with the leading `?`), skipping `undefined`, `null`
 * and empty strings, and keeping the given key order. Returns `''` when no
 * parameter remains.
 */
export function buildQueryString(params: Readonly<Record<string, QueryValue>>): string {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') {
      continue;
    }

    search.append(key, String(value));
  }

  const text = search.toString();
  return text === '' ? '' : `?${text}`;
}
