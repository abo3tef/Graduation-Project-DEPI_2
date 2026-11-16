'use client';

import {useCallback} from 'react';
import {useQueryClient} from '@tanstack/react-query';
import {useRouter} from '@/i18n/navigation';
import {clearSession} from '../session/session-store';

/**
 * Signs the user out: drops the stored session, clears every cached server response (so the next
 * account can never read the previous one's data) and returns to the login screen (FR-03).
 */
export function useSignOut(): () => void {
  const queryClient = useQueryClient();
  const router = useRouter();

  return useCallback(() => {
    clearSession();
    queryClient.clear();
    router.replace('/login');
  }, [queryClient, router]);
}
