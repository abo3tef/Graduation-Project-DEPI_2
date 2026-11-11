/**
 * Minimal RFC 7807 shape used by the Medilog Web API (`application/problem+json`).
 * `errors` stays `unknown` here; `toFieldErrors` is the only place that trusts its values.
 */
export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, unknown>;
};

/** Field name (camelCase) → messages, ready to be attached to a form control. */
export type FieldErrors = Record<string, string[]>;

export function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return false;
  }

  const candidate = value as Record<string, unknown>;

  if (candidate.status !== undefined && typeof candidate.status !== 'number') {
    return false;
  }

  if (candidate.title !== undefined && typeof candidate.title !== 'string') {
    return false;
  }

  if (
    candidate.errors !== undefined &&
    (typeof candidate.errors !== 'object' ||
      candidate.errors === null ||
      Array.isArray(candidate.errors))
  ) {
    return false;
  }

  return true;
}

/**
 * Normalises ASP.NET Core `ValidationProblemDetails.errors` into camelCase keys so
 * `MedicationName` and `medicationName` land on the same form field.
 */
export function toFieldErrors(problem: ProblemDetails | null): FieldErrors {
  if (!problem?.errors) {
    return {};
  }

  const fieldErrors: FieldErrors = {};

  for (const [key, value] of Object.entries(problem.errors)) {
    if (!Array.isArray(value)) {
      continue;
    }

    const messages = value.filter(
      (message): message is string =>
        typeof message === 'string' && message.trim().length > 0
    );

    if (messages.length === 0) {
      continue;
    }

    const field = toFieldName(key);
    fieldErrors[field] = [...(fieldErrors[field] ?? []), ...messages];
  }

  return fieldErrors;
}

function toFieldName(key: string): string {
  const lastSegment = key.split('.').pop() ?? key;

  if (lastSegment.length === 0) {
    return key;
  }

  return lastSegment.charAt(0).toLowerCase() + lastSegment.slice(1);
}
