'use client';

import {useState} from 'react';
import {FiEdit3, FiPlus} from 'react-icons/fi';
import {useFormatter, useTranslations} from 'next-intl';
import type {DiagnosisResponse} from '../types';
import {parseDateOnlyUtc} from '../validation';
import {DiagnosisForm} from './DiagnosisForm';
import {sectionActionClassName} from './form-styles';

type DiagnosisSectionProps = {
  visitId: string;
  diagnoses: DiagnosisResponse[];
};

/**
 * Diagnosis list for a visit. It renders the server's order as-is (diagnosisDate, then id) and
 * only ever holds local "which item is being edited" state; the list itself comes back from the
 * invalidated visit query after each mutation.
 */
export function DiagnosisSection({visitId, diagnoses}: DiagnosisSectionProps) {
  const t = useTranslations('MedicalRecords.diagnosis');
  const formatter = useFormatter();
  const [isCreating, setIsCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  function closeForms() {
    setIsCreating(false);
    setEditingId(null);
  }

  function startCreate() {
    setEditingId(null);
    setIsCreating(true);
  }

  function startEdit(diagnosisId: string) {
    setIsCreating(false);
    setEditingId(diagnosisId);
  }

  function formatDate(value: string): string {
    const date = parseDateOnlyUtc(value);

    if (!date) {
      return t('notProvided');
    }

    // `DateOnly` from the API: format in UTC so the day never shifts with the browser timezone.
    return formatter.dateTime(date, {dateStyle: 'medium', timeZone: 'UTC'});
  }

  return (
    <section
      aria-labelledby="diagnoses-title"
      className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="diagnoses-title" className="text-lg font-bold sm:text-xl">
            {t('title')}
          </h2>
          <p className="mt-1 text-sm text-muted">{t('description')}</p>
        </div>

        {!isCreating ? (
          <button
            type="button"
            onClick={startCreate}
            className={sectionActionClassName}
          >
            <FiPlus aria-hidden className="size-4" />
            {t('addAction')}
          </button>
        ) : null}
      </div>

      <div className="mt-5 flex flex-col gap-3">
        {isCreating ? (
          <DiagnosisForm
            mode="create"
            visitId={visitId}
            onDone={closeForms}
            onCancel={closeForms}
          />
        ) : null}

        {diagnoses.length === 0 && !isCreating ? (
          <div className="rounded-xl border border-dashed border-border bg-background p-6 text-center">
            <p className="text-sm font-semibold text-foreground">{t('emptyTitle')}</p>
            <p className="mt-1 text-xs text-muted">{t('emptyDescription')}</p>
          </div>
        ) : null}

        {diagnoses.length > 0 ? (
          <ul className="flex flex-col gap-3">
            {diagnoses.map((diagnosis) => (
              <li
                key={diagnosis.id}
                className="rounded-xl border border-border bg-background p-4"
              >
                {editingId === diagnosis.id ? (
                  <DiagnosisForm
                    mode="edit"
                    visitId={visitId}
                    diagnosis={diagnosis}
                    onDone={closeForms}
                    onCancel={closeForms}
                  />
                ) : (
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <h3 className="text-sm font-bold text-foreground">
                        {diagnosis.diagnosisName}
                      </h3>
                      <p className="mt-1 text-xs text-muted">
                        {t('dateLabel')}: {formatDate(diagnosis.diagnosisDate)}
                      </p>
                      {diagnosis.description ? (
                        <p className="mt-2 whitespace-pre-line text-sm text-foreground">
                          {diagnosis.description}
                        </p>
                      ) : null}
                    </div>

                    <button
                      type="button"
                      onClick={() => startEdit(diagnosis.id)}
                      aria-label={`${t('editAction')}: ${diagnosis.diagnosisName}`}
                      className={sectionActionClassName}
                    >
                      <FiEdit3 aria-hidden className="size-4" />
                      {t('editAction')}
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        ) : null}
      </div>
    </section>
  );
}
