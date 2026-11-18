'use client';

import {getSessionState, useSession} from '@/features/auth/session';
import {readCredentialScope} from './credential-scope';

/** Read-only F6 session projection; owns no storage or transport provider. */
export function readMedicalToken(): string | null {
  return getSessionState().session?.accessToken ?? null;
}

export function useMedicalCredentials() {
  const session = useSession();
  return {
    token: session.session?.accessToken ?? null,
    isReady: session.status !== 'loading',
    credentialScope: readCredentialScope()
  };
}
