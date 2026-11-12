// Test-only bridge to the auth feature's dev identity module. Component tests are
// allowed to reach into the module the production code reaches through
// `@/features/auth`; production code must keep using the feature's public index.
export {registerDevAuthHeaders, setDevIdentity as setDevIdentityForTests} from '../../auth/dev-identity/dev-identity';
