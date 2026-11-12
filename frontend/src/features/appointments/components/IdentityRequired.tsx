'use client';

import {useTranslations} from 'next-intl';
import {isDevIdentityEnabled} from '@/features/auth';
import {Alert} from './primitives/Alert';

/**
 * Shown instead of the screen when no acting user is known, so no request is sent
 * only to be rejected with 403. In development it points at the "Acting as" chip.
 */
export function IdentityRequired() {
  const t = useTranslations('Appointments.states');

  return isDevIdentityEnabled() ? (
    <Alert tone="info" title={t('identityTitle')}>
      {t('identityHint')}
    </Alert>
  ) : (
    <Alert tone="warning" title={t('signInTitle')}>
      {t('signInHint')}
    </Alert>
  );
}
