/**
 * Date parsing helpers shared by the patient screens. `DateOnly` strings are
 * parsed as UTC so the rendered calendar day never shifts with the browser
 * timezone (the F4 screens follow the same rule).
 */
export function parseDateOnlyUtc(value: string): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return null;
  }

  const date = new Date(`${value}T00:00:00Z`);

  return Number.isNaN(date.getTime()) ? null : date;
}

export function parseInstant(value: string): Date | null {
  const date = new Date(value);

  return Number.isNaN(date.getTime()) ? null : date;
}
