'use client';

import {useMutation} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {useRouter} from '@/i18n/navigation';
import {isApiError} from '@/lib/api';
import {notify} from '@/lib/utils/toast';
import {authErrorMessageKey} from '../errors/authErrorMessageKey';
import {
  forgotPassword,
  login,
  register,
  resendVerification,
  resetPassword,
  signInWithGoogle,
  verifyEmail
} from '../api/auth.api';
import {resolveRedirectTarget} from '../routing/landing';
import {setSession, type AuthSession} from '../session/session-store';
import type {
  LoginInput,
  RegisterInput,
  ResetPasswordInput,
  VerifyEmailInput
} from '../types/auth.types';

/** Variables of a sign-in: the credentials plus the `?next=` value the guard stored. */
export type SignInVariables<TInput> = {
  input: TInput;
  next?: string | null;
};

/**
 * Signing in publishes the session (the single credential owner) and then routes to the role's
 * landing page, honouring `?next=` only when that role may open it. The redirect is UX; the API
 * still enforces access (AGENTS.md §5).
 */
function useSignInMutation<TInput>(
  mutationFn: (input: TInput) => Promise<AuthSession>
) {
  const router = useRouter();
  const errorMessages = useTranslations('Auth.errors');
  const toast = useTranslations('Auth.toast');

  return useMutation({
    mutationFn: (variables: SignInVariables<TInput>) => mutationFn(variables.input),
    onSuccess: (result, variables) => {
      setSession(result);
      notify.success(toast('signedIn'));
      router.replace(resolveRedirectTarget(result.user.role, variables.next));
    },
    onError: (error) => {
      notify.error(isApiError(error) ? errorMessages(authErrorMessageKey(error)) : errorMessages('unknown'));
    }
  });
}

export function useLogin() {
  return useSignInMutation<LoginInput>(login);
}

export function useGoogleSignIn() {
  return useSignInMutation<string>(signInWithGoogle);
}

export function useRegister() {
  const toast = useTranslations('Auth.toast');
  const errorMessages = useTranslations('Auth.errors');
  return useMutation({
    mutationFn: (input: RegisterInput) => register(input),
    onSuccess: () => notify.success(toast('accountCreated')),
    onError: (error) => notify.error(isApiError(error) ? errorMessages(authErrorMessageKey(error)) : errorMessages('unknown'))
  });
}

export function useVerifyEmail() {
  const toast = useTranslations('Auth.toast');
  const errorMessages = useTranslations('Auth.errors');
  return useMutation({
    mutationFn: (input: VerifyEmailInput) => verifyEmail(input),
    onSuccess: () => notify.success(toast('emailVerified')),
    onError: (error) => notify.error(isApiError(error) ? errorMessages(authErrorMessageKey(error)) : errorMessages('unknown'))
  });
}

export function useResendVerification() {
  const toast = useTranslations('Auth.toast');
  const errorMessages = useTranslations('Auth.errors');
  return useMutation({
    mutationFn: (email: string) => resendVerification(email),
    onSuccess: () => notify.success(toast('verificationSent')),
    onError: (error) => notify.error(isApiError(error) ? errorMessages(authErrorMessageKey(error)) : errorMessages('unknown'))
  });
}

export function useForgotPassword() {
  const toast = useTranslations('Auth.toast');
  const errorMessages = useTranslations('Auth.errors');
  return useMutation({
    mutationFn: (email: string) => forgotPassword(email),
    onSuccess: () => notify.success(toast('resetSent')),
    onError: (error) => notify.error(isApiError(error) ? errorMessages(authErrorMessageKey(error)) : errorMessages('unknown'))
  });
}

export function useResetPassword() {
  const toast = useTranslations('Auth.toast');
  const errorMessages = useTranslations('Auth.errors');
  return useMutation({
    mutationFn: (input: ResetPasswordInput) => resetPassword(input),
    onSuccess: () => notify.success(toast('passwordChanged')),
    onError: (error) => notify.error(isApiError(error) ? errorMessages(authErrorMessageKey(error)) : errorMessages('unknown'))
  });
}
