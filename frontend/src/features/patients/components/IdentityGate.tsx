'use client';

import {useTranslations} from 'next-intl';
import {isDevIdentityEnabled} from '@/features/auth';
import {Alert} from './primitives/Alert';

/**
 * Shown instead of the medical screens when no acting doctor is known, so no
 * medical request is sent only to be rejected. In development it points at the
 * "Acting as" chip.
 */
export function IdentityGate() {
  const t = useTranslations('Patients.states');

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
