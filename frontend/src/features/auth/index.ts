// Public API for the auth feature (F6-C2).
//
// Route files and other features import only this barrel. The single credential owner is
// `session/session-store.ts`; `registerSessionAuth()` publishes it to the API transport once, from
// the locale providers.
export {SessionBadge} from './components/SessionBadge';
export {SessionExpiryWatcher} from './components/SessionExpiryWatcher';
export {RequireRole} from './guards/RequireRole';
export {useSession} from './hooks/useSession';
export type {SessionView} from './hooks/useSession';
export {useActingIdentity} from './hooks/useActingIdentity';
export type {ActingIdentity, ActingRole} from './hooks/useActingIdentity';
export {useSignOut} from './hooks/useSignOut';
export {registerSessionAuth} from './session/session-auth';
export {
  isPathAllowedForRole,
  landingPathFor,
  resolveRedirectTarget
} from './routing/landing';
export type {
  AuthSession,
  SessionState,
  SessionUser,
  SessionUserRole
} from './session/session-store';
