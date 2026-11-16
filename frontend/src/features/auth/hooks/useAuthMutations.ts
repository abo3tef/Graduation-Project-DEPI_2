'use client';

import {useMutation} from '@tanstack/react-query';
import {useRouter} from '@/i18n/navigation';
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

  return useMutation({
    mutationFn: (variables: SignInVariables<TInput>) => mutationFn(variables.input),
    onSuccess: (result, variables) => {
      setSession(result);
      router.replace(resolveRedirectTarget(result.user.role, variables.next));
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
  return useMutation({mutationFn: (input: RegisterInput) => register(input)});
}

export function useVerifyEmail() {
  return useMutation({mutationFn: (input: VerifyEmailInput) => verifyEmail(input)});
}

export function useResendVerification() {
  return useMutation({mutationFn: (email: string) => resendVerification(email)});
}

export function useForgotPassword() {
  return useMutation({mutationFn: (email: string) => forgotPassword(email)});
}

export function useResetPassword() {
  return useMutation({mutationFn: (input: ResetPasswordInput) => resetPassword(input)});
}
