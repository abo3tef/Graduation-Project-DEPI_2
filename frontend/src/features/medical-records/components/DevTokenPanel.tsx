'use client';

import {useState, type FormEvent} from 'react';
import {FiCheck, FiKey, FiTrash2} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {notify} from '@/lib/utils/toast';
import {useDevToken} from '../use-dev-token';

/**
 * Development-only token panel (D-28). It is never rendered in a production build, and the
 * token itself is never echoed back to the DOM — only “set / not set” is shown.
 */
export function DevTokenPanel() {
  if (process.env.NODE_ENV !== 'development') {
    return null;
  }

  return <DevTokenPanelContent />;
}

function DevTokenPanelContent() {
  const t = useTranslations('MedicalRecords.token');
  const {token, isReady, saveToken, clearToken} = useDevToken();
  const [value, setValue] = useState('');

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (value.trim().length === 0) {
      return;
    }

    if (!saveToken(value)) {
      notify.error(t('storageFailed'));
      return;
    }

    setValue('');
    notify.success(t('saved'));
  }

  function handleClear() {
    clearToken();
    notify.info(t('cleared'));
  }

  const statusLabel = !isReady
    ? t('statusChecking')
    : token
      ? t('statusSet')
      : t('statusMissing');

  return (
    <section
      aria-labelledby="dev-token-title"
      className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2
          id="dev-token-title"
          className="flex items-center gap-2 text-sm font-bold"
        >
          <FiKey aria-hidden className="size-4 text-brand" />
          {t('title')}
        </h2>
        <span
          className={
            isReady && token
              ? 'rounded-full bg-brand-soft px-3 py-1 text-xs font-semibold text-brand'
              : 'rounded-full bg-surface-2 px-3 py-1 text-xs font-semibold text-muted'
          }
        >
          {statusLabel}
        </span>
      </div>

      <p className="mt-2 text-sm leading-relaxed text-muted">{t('description')}</p>

      <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-3">
        <label className="text-sm font-medium" htmlFor="dev-token-input">
          {t('label')}
        </label>
        <input
          id="dev-token-input"
          type="password"
          dir="ltr"
          autoComplete="off"
          spellCheck={false}
          value={value}
          onChange={(event) => setValue(event.target.value)}
          placeholder={t('placeholder')}
          className="w-full rounded-xl border border-border bg-background px-3 py-2 font-mono text-sm text-foreground outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30"
        />

        <div className="flex flex-wrap gap-2">
          <button
            type="submit"
            disabled={value.trim().length === 0}
            className="inline-flex items-center gap-2 rounded-xl bg-brand px-4 py-2 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
          >
            <FiCheck aria-hidden className="size-4" />
            {t('save')}
          </button>
          <button
            type="button"
            onClick={handleClear}
            disabled={!isReady || !token}
            className="inline-flex items-center gap-2 rounded-xl border border-border px-4 py-2 text-sm font-semibold text-muted transition-colors hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50"
          >
            <FiTrash2 aria-hidden className="size-4" />
            {t('clear')}
          </button>
        </div>
      </form>
    </section>
  );
}
