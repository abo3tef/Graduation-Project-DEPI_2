export {apiRequest, apiRequestVoid, getApiBaseUrl} from './client';
export type {ApiRequestOptions, HttpMethod} from './client';
// `request` / `requestVoid` are the same transport under the names the
// medical-records feature already uses.
export {apiRequest as request, apiRequestVoid as requestVoid} from './client';
export {
  ApiError,
  createInvalidResponseError,
  createNetworkError,
  isApiError,
  kindFromStatus,
  normalizeFieldKey,
  parseProblem
} from './errors';
export type {ApiErrorKind, FieldErrors} from './errors';
export {
  getAuthHeaders,
  registerAuthHeadersProvider,
  resetAuthHeadersProviders
} from './auth-headers';
export type {AuthHeaders, AuthHeadersProvider} from './auth-headers';
export {buildQueryString} from './query-string';
export type {QueryValue} from './query-string';
export {readPagedResult} from './paging';
