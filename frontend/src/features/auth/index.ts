// Public API for the auth feature.
// Today it only contains the TEMPORARY development identity (STATE.md Q-07):
// real authentication will replace `registerDevAuthHeaders` and `useActingIdentity`.
export {DevIdentityChip} from './components/DevIdentityChip';
export {registerDevAuthHeaders, isDevIdentityEnabled} from './dev-identity/dev-identity';
export {useActingIdentity} from './hooks/useActingIdentity';
export type {ActingIdentity, ActingRole} from './hooks/useActingIdentity';
