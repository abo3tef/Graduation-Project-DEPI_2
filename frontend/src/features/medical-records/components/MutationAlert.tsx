'use client';

import {useTranslations} from 'next-intl';
import {toMutationFailureKind, type MutationFailureKind} from '../errors';

type MutationAlertProps = {
  error: unknown;
};

/**
 * Form-level failure banner. It renders the translated message for the normalised `ApiError`
 * kind, never the server `title`/`detail` text, so no backend wording leaks into the UI.
 */
export function MutationAlert({error}: MutationAlertProps) {
  const t = useTranslations('MedicalRecords.errors');

  if (!error) {
    return null;
  }

  const messages = {
    unauthorized: t('unauthorized'),
    forbidden: t('forbidden'),
    notFound: t('notFound'),
    conflict: t('conflict'),
    validation: t('validation'),
    network: t('network'),
    server: t('server')
  } satisfies Record<MutationFailureKind, string>;

  return (
    <p
      role="alert"
      className="rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm font-medium text-danger"
    >
      {messages[toMutationFailureKind(error)]}
    </p>
  );
}
