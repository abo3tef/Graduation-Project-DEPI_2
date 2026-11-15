'use client';

import {useState} from 'react';
import {FiActivity, FiAlertCircle, FiChevronDown, FiDroplet, FiFileText, FiArrowRight} from 'react-icons/fi';
import type {IconType} from 'react-icons';
import {useFormatter, useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import type {PatientTimelineEventResponse, PatientTimelineEventType} from '../types';

/** One icon per delivered event type (decision Q-20; vital signs join later via F10-C1-T4). */
const EVENT_ICONS: Readonly<Record<PatientTimelineEventType, IconType>> = {
  Visit: FiActivity,
  Diagnosis: FiAlertCircle,
  Medication: FiDroplet,
  MedicalNote: FiFileText
};

/** Translation-key segment per event type, used for the chip label. */
const EVENT_LABEL_KEYS: Readonly<Record<PatientTimelineEventType, string>> = {
  Visit: 'visit',
  Diagnosis: 'diagnosis',
  Medication: 'medication',
  MedicalNote: 'medicalNote'
};

type TimelineEventListProps = {
  /** Rendered in the exact order received — the API owns the timeline order. */
  events: readonly PatientTimelineEventResponse[];
};

/**
 * Chronological event cards. The backend contract already orders the events
 * (newest first with deterministic tie-breakers), so this component renders
 * the array as-is and never sorts on the client. Each card is a disclosure:
 * the header toggles the detail region built only from the payload's own
 * fields — no detail endpoint exists and none is invented.
 */
export function TimelineEventList({events}: TimelineEventListProps) {
  return (
    <ol className="flex flex-col gap-3">
      {events.map((event) => (
        <TimelineEventCard key={event.id} event={event} />
      ))}
    </ol>
  );
}

type TimelineEventCardProps = {
  event: PatientTimelineEventResponse;
};

function TimelineEventCard({event}: TimelineEventCardProps) {
  const t = useTranslations('MedicalTimeline.event');
  const formatter = useFormatter();
  const [isOpen, setIsOpen] = useState(false);

  const detailsId = `timeline-event-details-${event.id}`;
  const toggleId = `timeline-event-toggle-${event.id}`;
  const Icon = EVENT_ICONS[event.type];

  const formatOccurredAt = (value: string): string => {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return t('notProvided');
    }

    return formatter.dateTime(date, {dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC'});
  };

  return (
    <li className="rounded-2xl border border-border bg-surface p-4 sm:p-5">
      <h3 className="m-0">
        <button
          id={toggleId}
          type="button"
          aria-expanded={isOpen}
          aria-controls={detailsId}
          aria-label={t('detailsButton', {title: event.title})}
          onClick={() => setIsOpen((current) => !current)}
          className="flex w-full flex-wrap items-center gap-3 rounded-xl text-start outline-none transition-colors focus-visible:ring-2 focus-visible:ring-brand/40"
        >
          <span
            aria-hidden
            className="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand-soft text-brand"
          >
            <Icon className="size-4" />
          </span>
          <span className="rounded-full bg-surface-2 px-2.5 py-1 text-xs font-semibold text-muted">
            {t(`types.${EVENT_LABEL_KEYS[event.type]}`)}
          </span>
          <span className="font-bold text-foreground">{event.title}</span>
          <time dateTime={event.occurredAt} className="ms-auto text-xs text-muted">
            {formatOccurredAt(event.occurredAt)}
          </time>
          <FiChevronDown
            aria-hidden
            className={`size-4 shrink-0 text-muted transition-transform ${isOpen ? 'rotate-180' : ''}`}
          />
        </button>
      </h3>

      <div
        id={detailsId}
        role="region"
        aria-labelledby={toggleId}
        hidden={!isOpen}
        className="mt-3 border-t border-border pt-3"
      >
        <dl className="m-0 flex flex-col gap-2">
          <div className="flex flex-wrap gap-x-2 gap-y-0.5">
            <dt className="text-xs font-semibold text-muted">{t('typeLabel')}</dt>
            <dd className="m-0 text-sm text-foreground">
              {t(`types.${EVENT_LABEL_KEYS[event.type]}`)}
            </dd>
          </div>
          <div className="flex flex-wrap gap-x-2 gap-y-0.5">
            <dt className="text-xs font-semibold text-muted">{t('titleLabel')}</dt>
            <dd className="m-0 text-sm text-foreground">{event.title}</dd>
          </div>
          <div className="flex flex-wrap gap-x-2 gap-y-0.5">
            <dt className="text-xs font-semibold text-muted">{t('occurredAtLabel')}</dt>
            <dd className="m-0 text-sm text-foreground">
              <time dateTime={event.occurredAt}>{formatOccurredAt(event.occurredAt)}</time>
            </dd>
          </div>
          <div className="flex flex-wrap gap-x-2 gap-y-0.5">
            <dt className="text-xs font-semibold text-muted">{t('summaryLabel')}</dt>
            <dd className="m-0 text-sm leading-relaxed text-foreground">
              {event.summary ?? t('notProvided')}
            </dd>
          </div>

          {event.relatedVisitId !== null ? (
            <div className="flex flex-wrap gap-x-2 gap-y-0.5">
              <dt className="text-xs font-semibold text-muted">{t('relatedVisitLabel')}</dt>
              <dd className="m-0 flex flex-wrap items-center gap-x-2 gap-y-1">
                <span className="text-xs text-muted">
                  {t('relatedVisitIdLabel')}:
                </span>
                <code dir="ltr" className="text-xs text-foreground">
                  {event.relatedVisitId}
                </code>
                <Link
                  href={`/medical-records/visits/${event.relatedVisitId}`}
                  className="inline-flex items-center gap-1.5 text-sm font-semibold text-brand transition-colors hover:underline"
                >
                  {t('openRelatedVisit')}
                  <FiArrowRight aria-hidden className="size-3.5 rtl:rotate-180" />
                </Link>
              </dd>
            </div>
          ) : null}
        </dl>
      </div>
    </li>
  );
}
