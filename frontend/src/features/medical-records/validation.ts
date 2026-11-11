const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Route identifiers are GUIDs on the backend; reject malformed ids before calling the API. */
export function isGuid(value: string): boolean {
  return GUID_PATTERN.test(value.trim());
}
