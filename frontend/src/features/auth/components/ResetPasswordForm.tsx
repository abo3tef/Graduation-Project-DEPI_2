'use client';

import {useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {isApiError} from '@/lib/api';
import {authErrorMessageKey, authFieldErrors} from '../errors/authErrorMessageKey';
import {useResetPassword} from '../hooks/useAuthMutations';
import {
  RESET_FIELDS,
  hasErrors,
  validateResetForm,
  type AuthFieldErrors,
  type ResetField
} from '../validation/auth.validation';
import {AuthCard, FormAlert, SubmitButton} from './AuthCard';
import {PasswordField} from './PasswordField';
import {linkClassName} from './auth-styles';

type ResetPasswordFormProps = {
  /** The single-use token from the emailed link (`?token=`), absent when the link is broken. */
  token?: string | null;
};

/** New-password screen (FR-04). The token is sent to the API once; the policy matches registration. */
export function ResetPasswordForm({token = null}: ResetPasswordFormProps) {
  const t = useTranslations('Auth');
  const common = useTranslations('Auth.common');
  const errorMessages = useTranslations('Auth.errors');
  const reset = useResetPassword();
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errors, setErrors] = useState<AuthFieldErrors<ResetField>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  if (!token) {
    return (
      <AuthCard
        title={t('reset.title')}
        footer={
          <Link href="/forgot-password" className={linkClassName}>
            {t('reset.requestNew')}
          </Link>
        }
      >
        <FormAlert>{t('reset.missingToken')}</FormAlert>
      </AuthCard>
    );
  }

  if (done) {
    return (
      <AuthCard
        title={t('reset.successTitle')}
        footer={
          <Link href="/login" className={linkClassName}>
            {t('common.backToLogin')}
          </Link>
        }
      >
        <FormAlert tone="success">{t('reset.success')}</FormAlert>
      </AuthCard>
    );
  }

  function onSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();

    const nextErrors = validateResetForm({password, confirmPassword});
    setErrors(nextErrors);
    setFormError(null);

    if (hasErrors(nextErrors) || !token) {
      return;
    }

    reset.mutate(
      {token, newPassword: password},
      {
        onSuccess: () => setDone(true),
        onError: (error) => {
          const mapped = isApiError(error)
            ? authFieldErrors(error.fieldErrors, error.fieldErrorCodes, RESET_FIELDS)
            : {};
          setErrors(mapped);

          if (!hasErrors(mapped)) {
            setFormError(errorMessages(authErrorMessageKey(error)));
          }
        }
      }
    );
  }

  return (
    <AuthCard
      title={t('reset.title')}
      subtitle={t('reset.subtitle')}
      footer={
        <Link href="/login" className={linkClassName}>
          {t('common.backToLogin')}
        </Link>
      }
    >
      {formError ? <FormAlert>{formError}</FormAlert> : null}

      <form onSubmit={onSubmit} noValidate className="grid gap-4">
        <PasswordField
          label={common('newPassword')}
          name="password"
          autoComplete="new-password"
          value={password}
          onChange={setPassword}
          error={errors.password ? common(errors.password) : undefined}
          hint={common('passwordPolicy')}
        />
        <PasswordField
          label={common('confirmPassword')}
          name="confirmPassword"
          autoComplete="new-password"
          value={confirmPassword}
          onChange={setConfirmPassword}
          error={errors.confirmPassword ? common(errors.confirmPassword) : undefined}
        />
        <SubmitButton
          pending={reset.isPending}
          label={t('reset.submit')}
          pendingLabel={common('submitting')}
        />
      </form>
    </AuthCard>
  );
}
