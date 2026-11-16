'use client';

import {useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {authErrorMessageKey} from '../errors/authErrorMessageKey';
import {useForgotPassword} from '../hooks/useAuthMutations';
import {isValidEmail} from '../validation/auth.validation';
import {AuthCard, FormAlert, SubmitButton} from './AuthCard';
import {AuthField} from './AuthField';
import {linkClassName} from './auth-styles';

/**
 * Forgot-password screen (FR-04). The answer is deliberately identical whether or not the address
 * has an account, so it cannot be used to discover registered emails (AGENTS.md §5).
 */
export function ForgotPasswordForm() {
  const t = useTranslations('Auth');
  const common = useTranslations('Auth.common');
  const errorMessages = useTranslations('Auth.errors');
  const forgot = useForgotPassword();
  const [email, setEmail] = useState('');
  const [emailError, setEmailError] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  const loginLink = (
    <Link href="/login" className={linkClassName}>
      {t('common.backToLogin')}
    </Link>
  );

  if (sent) {
    return (
      <AuthCard title={t('forgot.sentTitle')} footer={loginLink}>
        <FormAlert tone="success">{t('forgot.sent')}</FormAlert>
      </AuthCard>
    );
  }

  function onSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    setFormError(null);

    const value = email.trim();

    if (value === '') {
      setEmailError(common('required'));
      return;
    }

    if (!isValidEmail(value)) {
      setEmailError(common('invalidEmail'));
      return;
    }

    setEmailError(null);
    forgot.mutate(value, {
      onSuccess: () => setSent(true),
      onError: (error) => setFormError(errorMessages(authErrorMessageKey(error)))
    });
  }

  return (
    <AuthCard title={t('forgot.title')} subtitle={t('forgot.subtitle')} footer={loginLink}>
      {formError ? <FormAlert>{formError}</FormAlert> : null}

      <form onSubmit={onSubmit} noValidate className="grid gap-4">
        <AuthField
          label={common('email')}
          name="email"
          type="email"
          inputMode="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
          error={emailError ?? undefined}
        />
        <SubmitButton
          pending={forgot.isPending}
          label={t('forgot.submit')}
          pendingLabel={common('submitting')}
        />
      </form>
    </AuthCard>
  );
}
