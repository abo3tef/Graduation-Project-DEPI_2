'use client';

import {useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {isApiError} from '@/lib/api';
import {authErrorMessageKey, authFieldErrors} from '../errors/authErrorMessageKey';
import {useRegister, useResendVerification} from '../hooks/useAuthMutations';
import {
  REGISTER_FIELDS,
  hasErrors,
  validateRegisterForm,
  type AuthFieldErrors,
  type RegisterField,
  type RegisterFormValues
} from '../validation/auth.validation';
import {REGISTER_ROLES, type AccountSummary, type RegisterRole} from '../types/auth.types';
import {AuthCard, FormAlert, SubmitButton} from './AuthCard';
import {AuthField} from './AuthField';
import {GoogleSignInButton} from './GoogleSignInButton';
import {PasswordField} from './PasswordField';
import {
  fieldErrorClassName,
  hintClassName,
  linkClassName,
  secondaryButtonClassName
} from './auth-styles';

const EMPTY_FORM: RegisterFormValues = {
  fullName: '',
  email: '',
  password: '',
  confirmPassword: '',
  phone: '',
  specialization: '',
  role: 'Doctor'
};

/**
 * Registration screen (FR-01). The role is a closed choice of Doctor or Secretary; an Admin can
 * never be created here (AGENTS.md §5). Success shows the "check your inbox" state instead of
 * signing the user in, because the account stays inactive until the emailed link is opened (Q-11).
 */
export function RegisterForm() {
  const t = useTranslations('Auth');
  const common = useTranslations('Auth.common');
  const errorMessages = useTranslations('Auth.errors');
  const registration = useRegister();
  const resend = useResendVerification();
  const [values, setValues] = useState<RegisterFormValues>(EMPTY_FORM);
  const [errors, setErrors] = useState<AuthFieldErrors<RegisterField>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [created, setCreated] = useState<AccountSummary | null>(null);
  const [resent, setResent] = useState(false);
  const [resendError, setResendError] = useState<string | null>(null);

  function update<K extends keyof RegisterFormValues>(key: K, value: RegisterFormValues[K]): void {
    setValues((current) => ({...current, [key]: value}));
  }

  function onResend(email: string): void {
    setResent(false);
    setResendError(null);
    resend.mutate(email, {
      onSuccess: () => setResent(true),
      onError: (failure) => setResendError(errorMessages(authErrorMessageKey(failure)))
    });
  }

  if (created) {
    return (
      <AuthCard
        title={t('register.checkEmailTitle')}
        footer={
          <Link href="/login" className={linkClassName}>
            {t('register.signIn')}
          </Link>
        }
      >
        <FormAlert tone="success">{t('register.checkEmail', {email: created.email})}</FormAlert>
        <div className="grid gap-2">
          <button
            type="button"
            disabled={resend.isPending}
            onClick={() => onResend(created.email)}
            className={`${secondaryButtonClassName} w-full`}
          >
            {resend.isPending ? common('submitting') : t('verify.resend')}
          </button>
          {resent ? <FormAlert tone="success">{t('verify.resent')}</FormAlert> : null}
          {resendError ? <FormAlert>{resendError}</FormAlert> : null}
        </div>
      </AuthCard>
    );
  }

  function onSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();

    const nextErrors = validateRegisterForm(values);
    setErrors(nextErrors);
    setFormError(null);

    if (hasErrors(nextErrors)) {
      return;
    }

    registration.mutate(
      {
        fullName: values.fullName,
        email: values.email,
        password: values.password,
        phone: values.phone,
        role: values.role as RegisterRole,
        specialization: values.specialization
      },
      {
        onSuccess: (summary) => setCreated(summary),
        onError: (error) => {
          const mapped = isApiError(error)
            ? authFieldErrors(error.fieldErrors, error.fieldErrorCodes, REGISTER_FIELDS)
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
      title={t('register.title')}
      subtitle={t('register.subtitle')}
      footer={
        <>
          {t('register.haveAccount')}{' '}
          <Link href="/login" className={linkClassName}>
            {t('register.signIn')}
          </Link>
        </>
      }
    >
      {formError ? <FormAlert>{formError}</FormAlert> : null}

      <div className="mb-4">
        <GoogleSignInButton onError={setFormError} />
      </div>

      <div className="relative my-4 flex items-center justify-center">
        <div className="absolute inset-0 flex items-center">
          <div className="w-full border-t border-border" />
        </div>
        <span className="relative bg-surface px-2 text-xs uppercase tracking-wider text-muted">
          {common('or')}
        </span>
      </div>

      <form onSubmit={onSubmit} noValidate className="grid gap-4">
        <AuthField
          label={common('fullName')}
          name="fullName"
          autoComplete="name"
          value={values.fullName}
          onChange={(value) => update('fullName', value)}
          error={errors.fullName ? common(errors.fullName) : undefined}
        />
        <AuthField
          label={common('email')}
          name="email"
          type="email"
          inputMode="email"
          autoComplete="email"
          value={values.email}
          onChange={(value) => update('email', value)}
          error={errors.email ? common(errors.email) : undefined}
        />
        <PasswordField
          label={common('password')}
          name="password"
          autoComplete="new-password"
          value={values.password}
          onChange={(value) => update('password', value)}
          error={errors.password ? common(errors.password) : undefined}
          hint={common('passwordPolicy')}
        />
        <PasswordField
          label={common('confirmPassword')}
          name="confirmPassword"
          autoComplete="new-password"
          value={values.confirmPassword}
          onChange={(value) => update('confirmPassword', value)}
          error={errors.confirmPassword ? common(errors.confirmPassword) : undefined}
        />

        <fieldset>
          <legend className="mb-1 block text-xs font-bold text-foreground">{common('role')}</legend>
          <div className="flex flex-wrap gap-2">
            {REGISTER_ROLES.map((role) => (
              <label
                key={role}
                className={`inline-flex min-h-9 items-center gap-2 rounded-xl border px-3 text-sm font-semibold ${
                  values.role === role ? 'border-brand bg-brand-soft' : 'border-border bg-background'
                }`}
              >
                <input
                  type="radio"
                  name="role"
                  value={role}
                  checked={values.role === role}
                  onChange={() => update('role', role)}
                  className="accent-brand"
                />
                {t(`session.role.${role}`)}
              </label>
            ))}
          </div>
          {errors.role ? <p className={fieldErrorClassName}>{common(errors.role)}</p> : null}
          <p className={hintClassName}>{t('register.roleHint')}</p>
        </fieldset>

        <AuthField
          label={common('phone')}
          name="phone"
          type="tel"
          inputMode="tel"
          autoComplete="tel"
          required={false}
          value={values.phone}
          onChange={(value) => update('phone', value)}
          error={errors.phone ? common(errors.phone) : undefined}
        />

        {values.role === 'Doctor' ? (
          <AuthField
            label={common('specialization')}
            name="specialization"
            required={false}
            value={values.specialization}
            onChange={(value) => update('specialization', value)}
            error={errors.specialization ? common(errors.specialization) : undefined}
          />
        ) : (
          <input type="hidden" name="specialization" value="" />
        )}

        <SubmitButton
          pending={registration.isPending}
          label={t('register.submit')}
          pendingLabel={common('submitting')}
        />
      </form>
    </AuthCard>
  );
}
