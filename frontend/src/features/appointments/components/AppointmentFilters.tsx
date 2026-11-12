'use client';

import {useTranslations} from 'next-intl';
import type {AppointmentFiltersController} from '../hooks/useAppointmentFilters';
import {
  APPOINTMENT_STATUSES,
  isAppointmentStatus,
  type DoctorLookup
} from '../types/appointment.types';
import {Button} from './primitives/Button';
import {controlClass, Field, fieldAria} from './primitives/Field';

type AppointmentFiltersProps = {
  controller: AppointmentFiltersController;
  doctors: readonly DoctorLookup[];
  doctorsLoading: boolean;
};

export function AppointmentFilters({controller, doctors, doctorsLoading}: AppointmentFiltersProps) {
  const t = useTranslations('Appointments');
  const {state, invalidRange} = controller;

  return (
    <form
      role="search"
      aria-label={t('filters.label')}
      onSubmit={(event) => event.preventDefault()}
      className="grid gap-4 rounded-2xl border border-border bg-surface p-4 sm:grid-cols-2 lg:grid-cols-4"
    >
      <Field id="filter-doctor" label={t('filters.doctor')}>
        <select
          value={state.doctorId}
          disabled={doctorsLoading}
          onChange={(event) => controller.setDoctorId(event.target.value)}
          className={controlClass}
          {...fieldAria('filter-doctor')}
        >
          <option value="">{t('filters.allDoctors')}</option>
          {doctors.map((doctor) => (
            <option key={doctor.id} value={doctor.id}>
              {doctor.fullName}
            </option>
          ))}
        </select>
      </Field>

      <Field id="filter-status" label={t('filters.status')}>
        <select
          value={state.status}
          onChange={(event) => {
            const value = event.target.value;
            controller.setStatus(isAppointmentStatus(value) ? value : 'all');
          }}
          className={controlClass}
          {...fieldAria('filter-status')}
        >
          <option value="all">{t('filters.allStatuses')}</option>
          {APPOINTMENT_STATUSES.map((status) => (
            <option key={status} value={status}>
              {t(`status.${status}`)}
            </option>
          ))}
        </select>
      </Field>

      <Field id="filter-date-from" label={t('filters.dateFrom')}>
        <input
          type="date"
          value={state.dateFrom}
          onChange={(event) => controller.setDateFrom(event.target.value)}
          className={controlClass}
          {...fieldAria('filter-date-from')}
        />
      </Field>

      <Field
        id="filter-date-to"
        label={t('filters.dateTo')}
        error={invalidRange ? t('filters.invalidRange') : null}
      >
        <input
          type="date"
          value={state.dateTo}
          min={state.dateFrom || undefined}
          onChange={(event) => controller.setDateTo(event.target.value)}
          className={controlClass}
          {...fieldAria('filter-date-to', invalidRange ? t('filters.invalidRange') : null)}
        />
      </Field>

      {controller.hasChanges ? (
        <div className="sm:col-span-2 lg:col-span-4">
          <Button size="sm" variant="ghost" onClick={controller.reset}>
            {t('filters.reset')}
          </Button>
        </div>
      ) : null}
    </form>
  );
}
