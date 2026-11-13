import {ApiError, type FieldErrors} from '@/lib/api';

/** Failure kinds that a visit `GET` can produce, as translation-key segments. */
export type QueryFailureKind =
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'network'
  | 'server';

/** Failure kinds that a visit mutation can produce. */
export type MutationFailureKind = QueryFailureKind | 'conflict' | 'validation';

export function toQueryFailureKind(error: unknown): QueryFailureKind {
  if (!(error instanceof ApiError)) {
    return 'server';
  }

  switch (error.kind) {
    case 'unauthorized':
      return 'unauthorized';
    case 'forbidden':
      return 'forbidden';
    case 'not-found':
      return 'notFound';
    case 'network':
      return 'network';
    default:
      return 'server';
  }
}

export function toMutationFailureKind(error: unknown): MutationFailureKind {
  if (!(error instanceof ApiError)) {
    return 'server';
  }

  switch (error.kind) {
    case 'unauthorized':
      return 'unauthorized';
    case 'forbidden':
      return 'forbidden';
    case 'not-found':
      return 'notFound';
    case 'conflict':
      return 'conflict';
    case 'validation':
      return 'validation';
    case 'network':
      return 'network';
    default:
      return 'server';
  }
}

/** Server `ValidationProblemDetails` field errors, empty for any other failure. */
export function fieldErrorsOf(error: unknown): FieldErrors {
  return error instanceof ApiError ? error.fieldErrors : {};
}
