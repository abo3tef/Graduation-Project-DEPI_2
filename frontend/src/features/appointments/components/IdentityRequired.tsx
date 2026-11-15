'use client';

import {useTranslations} from 'next-intl';
import {Alert} from './primitives/Alert';

/**
 * Shown instead of the screen when nobody is signed in, so no request is sent only to be
 * rejected with 401 (F6-C2: real authentication replaced the development identity).
 */
export function IdentityRequired() {
  const t = useTranslations('Appointments.states');

  return (
    <Alert tone="info" title={t('identityTitle')}>
      {t('signInHint')}
    </Alert>
  );
}
