import {apiRequest, apiRequestVoid} from '@/lib/api';
import {
  readAccountSummary,
  readSignInResult,
  type AccountSummary,
  type LoginInput,
  type RegisterInput,
  type ResetPasswordInput,
  type SignInResult,
  type VerifyEmailInput
} from '../types/auth.types';

/**
 * The auth endpoints (F6-C1 contract). Every call goes through the single API transport, so the
 * base URL, the error shape and the anonymous-request behaviour stay identical to the rest of the
 * app. These routes never need a token; the transport only adds one when a session exists.
 */
const BASE_PATH = '/api/auth';

export async function login(input: LoginInput): Promise<SignInResult> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/login`, {
    method: 'POST',
    body: {email: input.email.trim(), password: input.password}
  });

  return readSignInResult(data);
}

export async function register(input: RegisterInput): Promise<AccountSummary> {
  const phone = input.phone?.trim() ?? '';
  const specialization = input.specialization?.trim() ?? '';
  const data = await apiRequest<unknown>(`${BASE_PATH}/register`, {
    method: 'POST',
    body: {
      fullName: input.fullName.trim(),
      email: input.email.trim(),
      password: input.password,
      phone: phone === '' ? null : phone,
      role: input.role,
      // The server rejects a specialization for a secretary, so it is not sent for one.
      specialization: input.role === 'Doctor' && specialization !== '' ? specialization : null
    }
  });

  return readAccountSummary(data);
}

export async function verifyEmail(input: VerifyEmailInput): Promise<void> {
  const email = input.email?.trim() ?? '';

  await apiRequestVoid(`${BASE_PATH}/verify-email`, {
    method: 'POST',
    // The address only lets the API echo a UX hint back; the single-use token is the authority.
    body: email === '' ? {token: input.token.trim()} : {token: input.token.trim(), email}
  });
}

export async function resendVerification(email: string): Promise<void> {
  await apiRequestVoid(`${BASE_PATH}/resend-verification`, {
    method: 'POST',
    body: {email: email.trim()}
  });
}

export async function forgotPassword(email: string): Promise<void> {
  await apiRequestVoid(`${BASE_PATH}/forgot-password`, {
    method: 'POST',
    body: {email: email.trim()}
  });
}

export async function resetPassword(input: ResetPasswordInput): Promise<void> {
  await apiRequestVoid(`${BASE_PATH}/reset-password`, {
    method: 'POST',
    body: {token: input.token.trim(), newPassword: input.newPassword}
  });
}

export async function signInWithGoogle(idToken: string): Promise<SignInResult> {
  const data = await apiRequest<unknown>(`${BASE_PATH}/google`, {
    method: 'POST',
    body: {idToken}
  });

  return readSignInResult(data);
}
