'use client';

import {useState} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import {usePatientNotes} from '../hooks/use-patient-notes';
import {getPatientErrorKey} from '../services/patient-errors';
import {parseInstant} from '../utils/format';
import {ErrorPanel} from './StatePanel';
import {SkeletonRows} from './primitives/Skeleton';
import {Pagination} from './primitives/Pagination';
import {VisitLink} from './VisitLink';
import type {PatientNote} from '../types';

/**
 * Tab ④: every clinical note of the patient — visit-linked and unlinked
 * (§3.3, newest first), paged per the shared Q-04 contract. The active-tab
 * rendering means this query runs only when the tab is first opened.
 */
export function PatientNotesSection({patientId}: {patientId: string}) {
  const t = useTranslations('Patients.notes');
  const tRoot = useTranslations('Patients');
  const [pageNumber, setPageNumber] = useState(1);
  const notes = usePatientNotes(patientId, pageNumber);

  if (notes.isPending) {
    return <SkeletonRows rows={3} label={t('loading')} />;
  }

  if (notes.isError) {
    return (
      <ErrorPanel
        title={t('errorTitle')}
        description={tRoot(`errorKey.${getPatientErrorKey(notes.error)}`)}
        retryLabel={tRoot('common.retry')}
        onRetry={() => notes.refetch()}
      />
    );
  }

  const page = notes.data;

  if (page.items.length === 0 && pageNumber === 1) {
    return (
      <p role="status" className="m-0 rounded-2xl border border-border bg-surface p-6 text-sm text-muted">
        {t('empty')}
      </p>
    );
  }

  return (
    <div className="grid gap-4" data-testid="patient-notes">
      {page.items.length === 0 ? (
        <p role="status" className="m-0 rounded-2xl border border-border bg-surface p-6 text-sm text-muted">
          {t('emptyBeyond')}
        </p>
      ) : (
        page.items.map((note) => <NoteCard key={note.id} note={note} />)
      )}

      <Pagination
        label={t('paginationLabel')}
        pageNumber={page.page}
        totalPages={page.totalPages}
        disabled={notes.isPlaceholderData}
        onChange={setPageNumber}
      />
    </div>
  );
}

function NoteCard({note}: {note: PatientNote}) {
  const t = useTranslations('Patients.notes');
  const formatter = useFormatter();

  const createdAt = parseInstant(note.createdAtUtc);

  return (
    <article className="grid gap-2 rounded-2xl border border-border bg-surface p-5">
      <p className="m-0 text-sm leading-relaxed text-foreground" dir="auto" data-testid="note-content">
        {note.noteContent}
      </p>

      <footer className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted">
        <span>
          <span dir="auto">{note.doctorName}</span>
          {' · '}
          {createdAt
            ? formatter.dateTime(createdAt, {dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC'})
            : note.createdAtUtc}
          {note.updatedAtUtc !== null ? ` · ${t('edited')}` : ''}
        </span>

        <VisitLink visitId={note.visitId} linkLabel={t('link')} unlinkedLabel={t('unlinked')} />
      </footer>
    </article>
  );
}
