'use client';

import {useEffect, useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {Link, useRouter} from '@/i18n/navigation';
import {isApiError} from '@/lib/api';
import {authErrorMessageKey, authFieldErrors} from '../errors/authErrorMessageKey';
import {useLogin, useResendVerification} from '../hooks/useAuthMutations';
import {useSession} from '../hooks/useSession';
import {landingPathFor} from '../routing/landing';
import {
  LOGIN_FIELDS,
  hasErrors,
  validateLoginForm,
  type AuthFieldErrors,
  type LoginField
} from '../validation/auth.validation';
import {AuthCard, FormAlert, SubmitButton} from './AuthCard';
import {AuthField} from './AuthField';
import {GoogleSignInButton} from './GoogleSignInButton';
import {PasswordField} from './PasswordField';
import {linkClassName, secondaryButtonClassName} from './auth-styles';

type LoginFormProps = {
  /** `?next=` stored by the route guard; honoured only when the role may open it. */
  next?: string | null;
  /** `?reason=expired` set by `SessionExpiryWatcher` after a rejected token. */
  expired?: boolean;
};

/** Sign-in screen (FR-02). Client-side only: the API decides whether the credentials are valid. */
export function LoginForm({next = null, expired = false}: LoginFormProps) {
  const t = useTranslations('Auth');
  const common = useTranslations('Auth.common');
  const errorMessages = useTranslations('Auth.errors');
  const router = useRouter();
  const session = useSession();
  const login = useLogin();
  const resend = useResendVerification();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<AuthFieldErrors<LoginField>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [unverified, setUnverified] = useState(false);
  const [resent, setResent] = useState(false);
  const [resendError, setResendError] = useState<string | null>(null);

  // A signed-in user who opens the login screen goes to their own landing page (D-C2-1).
  useEffect(() => {
    if (session.status === 'authenticated') {
      router.replace(landingPathFor(session.role));
    }
  }, [session.status, session.role, router]);

  if (session.status !== 'anonymous') {
    return (
      <AuthCard title={t('login.title')}>
        <p className="text-sm text-muted">{t('guard.loading')}</p>
      </AuthCard>
    );
  }

  function onSubmit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();

    const nextErrors = validateLoginForm({email, password});
    setErrors(nextErrors);
    setFormError(null);
    setUnverified(false);
    setResent(false);
    setResendError(null);

    if (hasErrors(nextErrors)) {
      return;
    }

    login.mutate(
      {input: {email, password}, next},
      {
        onError: (error) => {
          const mapped = isApiError(error)
            ? authFieldErrors(error.fieldErrors, error.fieldErrorCodes, LOGIN_FIELDS)
            : {};
          setErrors(mapped);

          if (!hasErrors(mapped)) {
            setFormError(errorMessages(authErrorMessageKey(error)));
          }

          // The user already typed the right address; offer a resend without leaving this screen.
          if (isApiError(error) && error.code === 'auth.email_unverified') {
            setUnverified(true);
          }
        }
      }
    );
  }

  function onResend(): void {
    setResent(false);
    setResendError(null);
    resend.mutate(email, {
      onSuccess: () => setResent(true),
      onError: (failure) => setResendError(errorMessages(authErrorMessageKey(failure)))
    });
  }

  return (
    <AuthCard
      title={t('login.title')}
      subtitle={t('login.subtitle')}
      footer={
        <>
          {t('login.noAccount')}{' '}
          <Link href="/register" className={linkClassName}>
            {t('login.createAccount')}
          </Link>
        </>
      }
    >
      {expired ? <FormAlert tone="info">{t('login.expiredNotice')}</FormAlert> : null}
      {formError ? <FormAlert>{formError}</FormAlert> : null}
      {unverified ? (
        <div className="mb-4 grid gap-2">
          <p className="text-xs text-muted">{t('login.verifyNeeded')}</p>
          <button
            type="button"
            disabled={resend.isPending || email.trim() === ''}
            onClick={onResend}
            className={`${secondaryButtonClassName} w-full`}
          >
            {resend.isPending ? common('submitting') : t('verify.resend')}
          </button>
          {resent ? <FormAlert tone="success">{t('verify.resent')}</FormAlert> : null}
          {resendError ? <FormAlert>{resendError}</FormAlert> : null}
        </div>
      ) : null}

      <div className="mb-4">
        <GoogleSignInButton next={next} onError={setFormError} />
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
          label={common('email')}
          name="email"
          type="email"
          inputMode="email"
          autoComplete="email"
          value={email}
          onChange={setEmail}
          error={errors.email ? common(errors.email) : undefined}
        />
        <PasswordField
          label={common('password')}
          name="password"
          autoComplete="current-password"
          value={password}
          onChange={setPassword}
          error={errors.password ? common(errors.password) : undefined}
        />

        <div className="-mt-1 text-end">
          <Link href="/forgot-password" className="text-xs font-semibold text-muted hover:text-brand">
            {t('login.forgot')}
          </Link>
        </div>

        <SubmitButton
          pending={login.isPending}
          label={t('login.submit')}
          pendingLabel={common('submitting')}
        />
      </form>
    </AuthCard>
  );
}
