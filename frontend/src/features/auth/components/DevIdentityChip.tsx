'use client';

import {useRef, useState, type FormEvent} from 'react';
import {FiUser} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {
  DEV_ROLES,
  isDevIdentityEnabled,
  isDevRole,
  isGuid,
  setDevIdentity
} from '../dev-identity/dev-identity';
import {useActingIdentity} from '../hooks/useActingIdentity';

type Draft = {userId: string; role: string};

/**
 * Development-only control (renders nothing unless the dev identity is enabled):
 * shows who the app is acting as and lets a developer paste a user id and pick a
 * role. Removed together with the dev identity when authentication arrives (Q-07).
 */
export function DevIdentityChip() {
  const t = useTranslations('DevIdentity');
  const identity = useActingIdentity();
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [draft, setDraft] = useState<Draft>({userId: '', role: DEV_ROLES[0]});
  const [errorKey, setErrorKey] = useState<'invalidId' | 'invalidRole' | null>(null);

  if (!isDevIdentityEnabled()) {
    return null;
  }

  function openDialog() {
    setDraft({
      userId: identity.status === 'ready' ? identity.userId : '',
      role: identity.status === 'ready' ? identity.role : DEV_ROLES[0]
    });
    setErrorKey(null);
    dialogRef.current?.showModal();
  }

  function closeDialog() {
    dialogRef.current?.close();
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const userId = draft.userId.trim();

    if (!isGuid(userId)) {
      setErrorKey('invalidId');
      return;
    }

    if (!isDevRole(draft.role)) {
      setErrorKey('invalidRole');
      return;
    }

    setDevIdentity({userId, role: draft.role});
    closeDialog();
  }

  function handleClear() {
    setDevIdentity(null);
    closeDialog();
  }

  const label =
    identity.status === 'ready'
      ? t('acting', {role: t(`roles.${identity.role}`), id: identity.userId.slice(0, 8)})
      : t('set');

  return (
    <>
      <button
        type="button"
        onClick={openDialog}
        className="inline-flex min-h-9 items-center gap-2 rounded-full border border-amber-400/50 bg-amber-400/10 px-3 text-xs font-bold text-amber-300 transition hover:bg-amber-400/20 [:root[data-theme=light]_&]:text-amber-800"
      >
        <FiUser aria-hidden className="size-3.5" />
        <span dir="auto">{label}</span>
      </button>

      <dialog
        ref={dialogRef}
        aria-labelledby="dev-identity-title"
        onClick={(event) => {
          if (event.target === event.currentTarget) {
            closeDialog();
          }
        }}
        className="m-auto w-[min(28rem,calc(100%-2rem))] rounded-2xl border border-border bg-surface p-0 text-foreground backdrop:bg-black/60"
      >
        <form onSubmit={handleSubmit} noValidate className="grid gap-4 p-5">
          <div>
            <h2 id="dev-identity-title" className="m-0 text-base font-black">
              {t('title')}
            </h2>
            <p className="m-0 mt-1 text-xs leading-6 text-muted">{t('description')}</p>
          </div>

          <div className="grid gap-1.5">
            <label htmlFor="dev-identity-id" className="text-xs font-bold">
              {t('userId')}
            </label>
            <input
              id="dev-identity-id"
              dir="ltr"
              value={draft.userId}
              onChange={(event) => setDraft({...draft, userId: event.target.value})}
              aria-invalid={errorKey === 'invalidId'}
              aria-describedby={errorKey ? 'dev-identity-error' : undefined}
              autoComplete="off"
              spellCheck={false}
              placeholder="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
              className="min-h-10 rounded-lg border border-border bg-background px-3 text-start text-sm text-foreground"
            />
          </div>

          <div className="grid gap-1.5">
            <label htmlFor="dev-identity-role" className="text-xs font-bold">
              {t('role')}
            </label>
            <select
              id="dev-identity-role"
              value={draft.role}
              onChange={(event) => setDraft({...draft, role: event.target.value})}
              className="min-h-10 rounded-lg border border-border bg-background px-3 text-sm text-foreground"
            >
              {DEV_ROLES.map((role) => (
                <option key={role} value={role}>
                  {t(`roles.${role}`)}
                </option>
              ))}
            </select>
          </div>

          {errorKey ? (
            <p id="dev-identity-error" role="alert" className="m-0 text-xs font-bold text-red-400">
              {t(`errors.${errorKey}`)}
            </p>
          ) : null}

          <div className="flex flex-wrap justify-end gap-2">
            <button
              type="button"
              onClick={handleClear}
              className="min-h-9 rounded-full border border-border px-4 text-xs text-muted transition hover:text-foreground"
            >
              {t('clear')}
            </button>
            <button
              type="button"
              onClick={closeDialog}
              className="min-h-9 rounded-full border border-border px-4 text-xs text-foreground transition hover:border-brand"
            >
              {t('cancel')}
            </button>
            <button
              type="submit"
              className="min-h-9 rounded-full bg-brand px-4 text-xs font-bold text-[#06211f] transition hover:opacity-90"
            >
              {t('save')}
            </button>
          </div>
        </form>
      </dialog>
    </>
  );
}
