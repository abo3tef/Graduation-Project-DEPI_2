const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Route identifiers are GUIDs on the backend; reject malformed ids before calling the API. */
export function isGuid(value: string): boolean {
  return GUID_PATTERN.test(value.trim());
}

/**
 * Search rules for the F1-C4 patient lookup, mirroring the backend validator
 * (`LookupLimits`: limit 1..20, search ≤ 100). The backend stays authoritative;
 * these constants only decide when a request is worth sending.
 */
export const PATIENT_SEARCH_MIN_LENGTH = 2;
export const PATIENT_SEARCH_LIMIT = 10;

/** Debounce before the finder issues a lookup request (F1's combobox uses the same value). */
export const PATIENT_SEARCH_DEBOUNCE_MS = 300;
