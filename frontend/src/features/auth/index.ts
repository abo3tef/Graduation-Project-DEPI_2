// Public API for the auth feature (F6-C2).
//
// Route files and other features import only this barrel. The single credential owner is
// `session/session-store.ts`; `registerSessionAuth()` publishes it to the API transport once, from
// the locale providers.
export {SessionBadge} from './components/SessionBadge';
export {SessionExpiryWatcher} from './components/SessionExpiryWatcher';
export {SignedInNotice} from './components/SignedInNotice';
export {LoginForm} from './components/LoginForm';
export {RegisterForm} from './components/RegisterForm';
export {VerifyEmailScreen} from './components/VerifyEmailScreen';
export {ForgotPasswordForm} from './components/ForgotPasswordForm';
export {ResetPasswordForm} from './components/ResetPasswordForm';
export {RequireRole} from './guards/RequireRole';
export {useSession} from './hooks/useSession';
export type {SessionView} from './hooks/useSession';
export {useActingIdentity} from './hooks/useActingIdentity';
export type {ActingIdentity, ActingRole} from './hooks/useActingIdentity';
export {useSignOut} from './hooks/useSignOut';
export {
  useForgotPassword,
  useGoogleSignIn,
  useLogin,
  useRegister,
  useResendVerification,
  useResetPassword,
  useVerifyEmail
} from './hooks/useAuthMutations';
export type {SignInVariables} from './hooks/useAuthMutations';
export {
  forgotPassword,
  login,
  register,
  resendVerification,
  resetPassword,
  signInWithGoogle,
  verifyEmail
} from './api/auth.api';
export {authErrorMessageKey, authFieldErrors} from './errors/authErrorMessageKey';
export type {AuthErrorKey} from './errors/authErrorMessageKey';
export {
  FULL_NAME_MAX_LENGTH,
  LOGIN_FIELDS,
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  REGISTER_FIELDS,
  RESET_FIELDS,
  hasErrors,
  isStrongPassword,
  isValidEmail,
  validateLoginForm,
  validateRegisterForm,
  validateResetForm
} from './validation/auth.validation';
export type {
  AuthField,
  AuthFieldErrors,
  AuthValidationKey,
  LoginField,
  LoginFormValues,
  RegisterField,
  RegisterFormValues,
  ResetField,
  ResetFormValues
} from './validation/auth.validation';
export {REGISTER_ROLES, isRegisterRole} from './types/auth.types';
export type {
  AccountSummary,
  AuthUser,
  AuthUserRole,
  LoginInput,
  RegisterInput,
  RegisterRole,
  ResetPasswordInput,
  SignInResult,
  VerifyEmailInput
} from './types/auth.types';
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
