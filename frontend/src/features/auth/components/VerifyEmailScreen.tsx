'use client';

import {useEffect, useRef, useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {authErrorMessageKey} from '../errors/authErrorMessageKey';
import {useResendVerification, useVerifyEmail} from '../hooks/useAuthMutations';
import {AuthCard, FormAlert, SubmitButton} from './AuthCard';
import {AuthField} from './AuthField';
import {linkClassName} from './auth-styles';

type VerifyEmailScreenProps = {
  /** The single-use token from the emailed link (`?token=`), absent when the link is broken. */
  token?: string | null;
};

type VerifyStatus = 'missing' | 'verifying' | 'success' | 'failed';

/**
 * Email-verification screen (FR-01, Q-11). Opening the emailed link verifies the account once on
 * mount; a broken or rejected link offers a new one and never reveals whether an address exists.
 */
export function VerifyEmailScreen({token = null}: VerifyEmailScreenProps) {
  const t = useTranslations('Auth');
  const common = useTranslations('Auth.common');
  const errorMessages = useTranslations('Auth.errors');
  const {mutate: verifyToken} = useVerifyEmail();
  const resend = useResendVerification();
  const [status, setStatus] = useState<VerifyStatus>(token ? 'verifying' : 'missing');
  const [error, setError] = useState<string | null>(null);
  const [email, setEmail] = useState('');
  const [emailError, setEmailError] = useState<string | null>(null);
  const [resent, setResent] = useState(false);
  const [resendError, setResendError] = useState<string | null>(null);
  const attempted = useRef(false);

  useEffect(() => {
    if (!token || attempted.current) {
      return;
    }

    attempted.current = true;
    verifyToken(
      {token},
      {
        onSuccess: () => setStatus('success'),
        onError: (failure) => {
          setStatus('failed');
          setError(errorMessages(authErrorMessageKey(failure)));
        }
      }
    );
  }, [token, verifyToken, errorMessages]);

  function onResend(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    setResendError(null);

    if (email.trim() === '') {
      setEmailError(common('required'));
      return;
    }

    setEmailError(null);
    resend.mutate(email, {
      onSuccess: () => setResent(true),
      onError: (failure) => setResendError(errorMessages(authErrorMessageKey(failure)))
    });
  }

  const resendForm = (
    <form onSubmit={onResend} noValidate className="mt-4 grid gap-3">
      <AuthField
        label={common('email')}
        name="email"
        type="email"
        inputMode="email"
        autoComplete="email"
        value={email}
        onChange={setEmail}
        error={emailError ?? undefined}
        hint={t('verify.resendNeedsEmail')}
      />
      <SubmitButton
        pending={resend.isPending}
        label={t('verify.resend')}
        pendingLabel={common('submitting')}
      />
      {resent ? <FormAlert tone="success">{t('verify.resent')}</FormAlert> : null}
      {resendError ? <FormAlert>{resendError}</FormAlert> : null}
    </form>
  );

  const loginLink = (
    <Link href="/login" className={linkClassName}>
      {t('verify.goToLogin')}
    </Link>
  );

  if (status === 'verifying') {
    return (
      <AuthCard title={t('verify.title')} subtitle={t('verify.description')}>
        <p role="status" className="text-sm text-muted">
          {common('submitting')}
        </p>
      </AuthCard>
    );
  }

  if (status === 'success') {
    return (
      <AuthCard title={t('verify.successTitle')} footer={loginLink}>
        <FormAlert tone="success">{t('verify.success')}</FormAlert>
      </AuthCard>
    );
  }

  return (
    <AuthCard title={t('verify.failedTitle')} footer={loginLink}>
      <FormAlert>{status === 'missing' ? t('verify.missingToken') : error ?? t('errors.tokenInvalid')}</FormAlert>
      {resendForm}
    </AuthCard>
  );
}
