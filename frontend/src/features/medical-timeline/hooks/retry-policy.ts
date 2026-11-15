import {ApiError} from '@/lib/api';

const NON_RETRYABLE_KINDS: readonly string[] = [
  'unauthorized',
  'forbidden',
  'not-found',
  'validation',
  'conflict'
];

/**
 * Shared retry policy for the doctor-scoped timeline queries, matching the
 * medical-records convention: permanent client errors are never retried;
 * anything else gets one retry.
 */
export function retryOnceExceptPermanent(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && NON_RETRYABLE_KINDS.includes(error.kind)) {
    return false;
  }

  return failureCount < 1;
}
