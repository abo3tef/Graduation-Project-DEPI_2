'use client';

import {useEffect, useState} from 'react';
import {FiPlus} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {Link} from '@/i18n/navigation';
import {useAppointmentFilters} from '../hooks/useAppointmentFilters';
import {useErrorMessage} from '../hooks/useAppointmentMessages';
import {useActor, useAppointmentList} from '../hooks/useAppointments';
import {useDoctorLookups} from '../hooks/useLookups';
import type {Appointment} from '../types/appointment.types';
import type {AppointmentAction} from '../utils/status-actions';
import {AppointmentFilters} from './AppointmentFilters';
import {AppointmentsTable} from './AppointmentsTable';
import {CancelDialog} from './CancelDialog';
import {IdentityRequired} from './IdentityRequired';
import {RescheduleDialog} from './RescheduleDialog';
import {StatusDialog, type StatusDialogState} from './StatusDialog';
import {Alert} from './primitives/Alert';
import {Button} from './primitives/Button';
import {Pagination} from './primitives/Pagination';
import {SkeletonRows} from './primitives/Skeleton';

const NEW_BUTTON_CLASS =
  'inline-flex min-h-10 items-center justify-center gap-2 rounded-full bg-brand px-5 text-sm font-bold text-[#06211f] transition hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand';

/** The secretary's appointment list: filters, results, pagination and row actions. */
export function AppointmentsPage() {
  const t = useTranslations('Appointments');
  const errorMessage = useErrorMessage();
  const {identity, ready} = useActor();
  const controller = useAppointmentFilters();
  const doctors = useDoctorLookups();
  const list = useAppointmentList(controller.filters, !controller.invalidRange);
  const [reschedule, setReschedule] = useState<Appointment | null>(null);
  const [cancel, setCancel] = useState<Appointment | null>(null);
  const [status, setStatus] = useState<StatusDialogState>(null);

  const page = list.data;
  const {setPageNumber} = controller;
  const pageNumber = controller.filters.pageNumber;

  // After a change the current page can disappear (for example the last item moved
  // away); step back to the last page that still exists instead of showing "empty".
  useEffect(() => {
    if (page && page.items.length === 0 && page.totalCount > 0 && pageNumber > page.totalPages) {
      setPageNumber(page.totalPages);
    }
  }, [page, pageNumber, setPageNumber]);

  function handleAction(action: AppointmentAction, appointment: Appointment) {
    switch (action) {
      case 'reschedule':
        setReschedule(appointment);
        break;
      case 'cancel':
        setCancel(appointment);
        break;
      case 'confirm':
        setStatus({appointment, status: 'Confirmed'});
        break;
      case 'noShow':
        setStatus({appointment, status: 'NoShow'});
        break;
      case 'complete':
        setStatus({appointment, status: 'Completed'});
        break;
    }
  }

  return (
    <div className="grid gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="m-0 text-2xl font-black">{t('title')}</h1>
          <p className="m-0 mt-1 text-sm text-muted">{t('subtitle')}</p>
        </div>
        {ready ? (
          <Link href="/appointments/new" className={NEW_BUTTON_CLASS}>
            <FiPlus aria-hidden className="size-4" />
            {t('actions.new')}
          </Link>
        ) : null}
      </header>

      {identity.status === 'loading' ? <SkeletonRows label={t('states.loading')} /> : null}
      {identity.status === 'none' ? <IdentityRequired /> : null}

      {ready ? (
        <>
          <AppointmentFilters
            controller={controller}
            doctors={doctors.data ?? []}
            doctorsLoading={doctors.isPending}
          />

          <section aria-labelledby="appointments-results" className="grid gap-4">
            <h2 id="appointments-results" className="sr-only">
              {t('results.heading')}
            </h2>

            {list.isPending && !controller.invalidRange ? (
              <SkeletonRows label={t('states.loading')} />
            ) : null}

            {list.isError ? (
              <Alert
                tone="error"
                title={t('states.errorTitle')}
                action={
                  <Button size="sm" onClick={() => void list.refetch()} loading={list.isFetching}>
                    {t('actions.retry')}
                  </Button>
                }
              >
                {errorMessage(list.error)}
              </Alert>
            ) : null}

            {page && page.items.length === 0 && page.totalCount === 0 ? (
              <div className="grid justify-items-center gap-3 rounded-2xl border border-dashed border-border bg-surface p-10 text-center">
                <p className="m-0 text-lg font-black">{t('states.emptyTitle')}</p>
                <p className="m-0 text-sm text-muted">{t('states.emptyHint')}</p>
                <div className="flex flex-wrap justify-center gap-2">
                  {controller.hasChanges ? (
                    <Button onClick={controller.reset}>{t('filters.reset')}</Button>
                  ) : null}
                  <Link href="/appointments/new" className={NEW_BUTTON_CLASS}>
                    <FiPlus aria-hidden className="size-4" />
                    {t('actions.new')}
                  </Link>
                </div>
              </div>
            ) : null}

            {page && page.items.length > 0 ? (
              <>
                <p className="m-0 text-xs text-muted" aria-live="polite">
                  {t('results.count', {count: page.totalCount})}
                </p>
                <AppointmentsTable
                  items={page.items}
                  role={identity.role}
                  onAction={handleAction}
                  busy={list.isFetching}
                />
                <Pagination
                  pageNumber={page.pageNumber}
                  totalPages={page.totalPages}
                  onChange={controller.setPageNumber}
                  disabled={list.isFetching}
                />
              </>
            ) : null}
          </section>
        </>
      ) : null}

      <RescheduleDialog appointment={reschedule} onClose={() => setReschedule(null)} />
      <CancelDialog appointment={cancel} onClose={() => setCancel(null)} />
      <StatusDialog state={status} onClose={() => setStatus(null)} />
    </div>
  );
}
