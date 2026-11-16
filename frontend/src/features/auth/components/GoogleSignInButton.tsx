'use client';

import {useState} from 'react';
import {useTranslations} from 'next-intl';
import {isApiError} from '@/lib/api';
import {authErrorMessageKey} from '../errors/authErrorMessageKey';
import {isFirebaseCancelled, isFirebaseConfigured, requestGoogleIdToken} from '../google/firebase-client';
import {useGoogleSignIn} from '../hooks/useAuthMutations';
import {FormAlert} from './AuthCard';

function GoogleIcon({className = 'size-4'}: {className?: string}) {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className={className}>
      <path
        fill="#4285F4"
        d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.82-2.4 3.68v3.05h3.88c2.27-2.09 3.66-5.17 3.66-9.17z"
      />
      <path
        fill="#34A853"
        d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.05c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.25v3.15C3.26 21.36 7.33 24 12 24z"
      />
      <path
        fill="#FBBC05"
        d="M5.28 14.27c-.25-.72-.38-1.49-.38-2.27s.13-1.55.38-2.27V6.58H1.25C.45 8.18 0 9.99 0 12s.45 3.82 1.25 5.42l4.03-3.15z"
      />
      <path
        fill="#EA4335"
        d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.33 0 3.26 2.64 1.25 6.58l4.03 3.15c.95-2.83 3.6-4.98 6.72-4.98z"
      />
    </svg>
  );
}

type GoogleSignInButtonProps = {
  /** `?next=` preserved for post-login redirect */
  next?: string | null;
  /** Callback for parent form to receive the error message */
  onError?: (message: string) => void;
};

/**
 * Google Sign-In button backed by Firebase Auth and POST /api/auth/google (F6-C2, Q-12).
 * Renders even when Firebase keys are absent, reporting an explanation when clicked.
 */
export function GoogleSignInButton({next = null, onError}: GoogleSignInButtonProps) {
  const t = useTranslations('Auth');
  const common = useTranslations('Auth.common');
  const errorMessages = useTranslations('Auth.errors');
  const googleSignIn = useGoogleSignIn();
  const [localError, setLocalError] = useState<string | null>(null);
  const [inFlight, setInFlight] = useState(false);

  function reportError(message: string): void {
    setLocalError(message);
    onError?.(message);
  }

  async function onClick(): Promise<void> {
    setLocalError(null);

    if (!isFirebaseConfigured()) {
      reportError(t('login.googleNotConfigured'));
      return;
    }

    setInFlight(true);
    try {
      const idToken = await requestGoogleIdToken();
      googleSignIn.mutate(
        {input: idToken, next},
        {
          onError: (error) => {
            const message = isApiError(error)
              ? errorMessages(authErrorMessageKey(error))
              : common('unexpected');
            reportError(message);
          },
          onSettled: () => {
            setInFlight(false);
          }
        }
      );
    } catch (error) {
      setInFlight(false);
      if (isFirebaseCancelled(error)) {
        reportError(t('login.googleCancelled'));
        return;
      }
      reportError(t('login.googleFailed'));
    }
  }

  const isPending = inFlight || googleSignIn.isPending;

  return (
    <div className="grid gap-2">
      <button
        type="button"
        onClick={onClick}
        disabled={isPending}
        className="inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl border border-border bg-surface px-4 py-2.5 text-sm font-semibold text-foreground shadow-sm transition hover:bg-surface-elevated hover:border-brand/40 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-50"
      >
        <GoogleIcon className="size-4 shrink-0" />
        <span>{isPending ? common('submitting') : t('login.google')}</span>
      </button>
      {!onError && localError ? <FormAlert>{localError}</FormAlert> : null}
    </div>
  );
}
