'use client';

import {useState, type FormEvent} from 'react';
import {FiArrowRight, FiClipboard} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {useRouter} from '@/i18n/navigation';
import {DevTokenPanel} from './DevTokenPanel';
import {useDevToken} from '../use-dev-token';
import {isGuid} from '../validation';

/**
 * Entry point for the medical-records feature.
 *
 * Until the shared list contract (Q-04) exists there is no visit browser, so the doctor opens a
 * known visit by its identifier (D-31). The token panel is the development bridge (D-28).
 */
export function MedicalRecordsWorkspace() {
  const t = useTranslations('MedicalRecords.workspace');
  const router = useRouter();
  const {token, isReady} = useDevToken();
  const [visitId, setVisitId] = useState('');
  const [error, setError] = useState<string | null>(null);

  const hasToken = isReady && token !== null;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const trimmed = visitId.trim();

    if (!isGuid(trimmed)) {
      setError(t('invalidVisitId'));
      return;
    }

    setError(null);
    router.push(`/medical-records/visits/${trimmed}`);
  }

  return (
    <main className="flex flex-col gap-6">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
          {t('eyebrow')}
        </p>
        <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('title')}</h1>
        <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">
          {t('description')}
        </p>
      </header>

      <div className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
        <section
          aria-labelledby="open-visit-title"
          className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
        >
          <h2
            id="open-visit-title"
            className="flex items-center gap-2 text-sm font-bold"
          >
            <FiClipboard aria-hidden className="size-4 text-brand" />
            {t('openVisitTitle')}
          </h2>
          <p className="mt-2 text-sm leading-relaxed text-muted">
            {t('openVisitHint')}
          </p>

          <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-3">
            <label className="text-sm font-medium" htmlFor="open-visit-id">
              {t('visitIdLabel')}
            </label>
            <input
              id="open-visit-id"
              type="text"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              value={visitId}
              onChange={(event) => {
                setVisitId(event.target.value);

                if (error) {
                  setError(null);
                }
              }}
              aria-invalid={error !== null}
              aria-describedby={error ? 'open-visit-error' : undefined}
              placeholder={t('visitIdPlaceholder')}
              className="w-full rounded-xl border border-border bg-background px-3 py-2 font-mono text-sm text-foreground outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30"
            />

            {error ? (
              <p id="open-visit-error" role="alert" className="text-sm text-danger">
                {error}
              </p>
            ) : null}

            {isReady && !hasToken ? (
              <p className="text-xs text-muted">{t('tokenRequired')}</p>
            ) : null}

            <button
              type="submit"
              disabled={!hasToken}
              className="inline-flex w-fit items-center gap-2 rounded-xl bg-brand px-4 py-2 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {t('openVisit')}
              <FiArrowRight aria-hidden className="size-4 rtl:rotate-180" />
            </button>
          </form>
        </section>

        <DevTokenPanel />
      </div>
    </main>
  );
}
