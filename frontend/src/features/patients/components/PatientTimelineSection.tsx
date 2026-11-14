'use client';

import {useState} from 'react';
import {useFormatter, useTranslations} from 'next-intl';
import {usePatientTimeline} from '../hooks/use-patient-timeline';
import {getPatientErrorKey} from '../services/patient-errors';
import {parseInstant} from '../utils/format';
import type {PatientTimelineEvent} from '../types';
import {ErrorPanel} from './StatePanel';
import {SkeletonRows} from './primitives/Skeleton';
import {Pagination} from './primitives/Pagination';
import {VisitLink} from './VisitLink';

export function PatientTimelineSection({patientId}: {patientId: string}) {
  const t = useTranslations('Patients.timeline');
  const root = useTranslations('Patients');
  const [pageNumber, setPageNumber] = useState(1);
  const query = usePatientTimeline(patientId, pageNumber);
  if (query.isPending) return <SkeletonRows rows={3} label={t('loading')} />;
  if (query.isError) return <ErrorPanel title={t('errorTitle')} description={root(`errorKey.${getPatientErrorKey(query.error)}`)} retryLabel={root('common.retry')} onRetry={() => query.refetch()} />;
  const page = query.data;
  if (page.items.length === 0 && pageNumber === 1) return <p role="status" className="rounded-2xl border border-border bg-surface p-6 text-muted">{t('empty')}</p>;
  return <div className="grid gap-4" data-testid="patient-timeline">
    {page.items.length === 0 ? <p role="status" className="rounded-2xl border border-border bg-surface p-6 text-muted">{t('emptyBeyond')}</p> : page.items.map(event => <TimelineCard event={event} key={`${event.eventType}-${event.id}`} />)}
    <Pagination label={t('paginationLabel')} pageNumber={page.page} totalPages={page.totalPages} disabled={query.isPlaceholderData} onChange={setPageNumber} />
  </div>;
}

function TimelineCard({event}: {event: PatientTimelineEvent}) {
  const t = useTranslations('Patients.timeline');
  const formatter = useFormatter();
  const instant = parseInstant(event.occurredAtUtc);
  return <article className="grid gap-2 rounded-2xl border border-border bg-surface p-5">
    <div className="flex flex-wrap items-baseline justify-between gap-2">
      <h3 className="font-black text-foreground">{t(`event.${event.eventType}`)}</h3>
      <time className="text-xs text-muted" dateTime={event.occurredAtUtc}>{instant ? formatter.dateTime(instant, {dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC'}) : event.occurredAtUtc}</time>
    </div>
    {event.visit && <div className="grid gap-1 text-sm"><p dir="auto">{event.visit.chiefComplaint ?? t('noComplaint')}</p><p className="text-muted" dir="auto">{event.visit.doctorName}</p><p className="text-muted">{t('counts', {diagnoses: event.visit.diagnosesCount, medications: event.visit.medicationsCount, notes: event.visit.notesCount, labs: event.visit.labResultsCount})}</p></div>}
    {event.note && <div className="grid gap-1"><p dir="auto" className="whitespace-pre-wrap break-words text-sm">{event.note.noteContent}</p><p className="text-xs text-muted" dir="auto">{event.note.doctorName}</p></div>}
    {event.labResult && <p className="text-sm" dir="auto">{event.labResult.testName}: <span dir="ltr">{event.labResult.value} {event.labResult.unit}</span></p>}
    {event.document && <div className="grid gap-1 text-sm"><p dir="auto">{event.document.fileName} ({event.document.fileType})</p>{event.document.description && <p dir="auto" className="text-muted">{event.document.description}</p>}{event.document.uploadedByDoctorName && <p dir="auto" className="text-xs text-muted">{event.document.uploadedByDoctorName}</p>}</div>}
    {event.eventType !== 'document' && <div className="pt-1"><VisitLink visitId={event.visitId} linkLabel={t('openVisit')} unlinkedLabel={t('unlinked')} /></div>}
  </article>;
}
