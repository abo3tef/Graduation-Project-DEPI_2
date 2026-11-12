'use client';

import {useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {isApiError} from '@/lib/api';
import {Link, useRouter} from '@/i18n/navigation';
import {
  focusFirstInvalid,
  useErrorMessage,
  useValidationMessage
} from '../hooks/useAppointmentMessages';
import {useActor, useBookAppointment} from '../hooks/useAppointments';
import {useDoctorLookups} from '../hooks/useLookups';
import {mapFieldErrors} from '../services/appointment-errors';
import {REASON_MAX_LENGTH, type PatientLookup} from '../types/appointment.types';
import {addMinutesToTime, getClinicNow} from '../utils/date-time';
import {
  hasErrors,
  validateBooking,
  type BookField,
  type FormErrors
} from '../validation/appointment.validation';
import {DaySchedulePanel} from './DaySchedulePanel';
import {IdentityRequired} from './IdentityRequired';
import {PatientCombobox} from './PatientCombobox';
import {Alert} from './primitives/Alert';
import {Button} from './primitives/Button';
import {controlClass, Field, fieldAria} from './primitives/Field';
import {SkeletonRows} from './primitives/Skeleton';

const FIELD_ORDER: readonly BookField[] = [
  'doctorId',
  'patientId',
  'appointmentDate',
  'startTime',
  'endTime',
  'reason'
];
const QUICK_DURATIONS = [15, 30, 45, 60] as const;
const fieldId = (field: BookField) => `booking-${field}`;

/** Booking page: doctor, patient, date, start/end, reason; with the doctor's day for context. */
export function BookingForm() {
  const t = useTranslations('Appointments');
  const router = useRouter();
  const message = useValidationMessage();
  const errorMessage = useErrorMessage();
  const {identity, ready} = useActor();
  const doctors = useDoctorLookups();
  const mutation = useBookAppointment();

  const [chosenDoctorId, setChosenDoctorId] = useState('');
  const [patient, setPatient] = useState<PatientLookup | null>(null);
  const [date, setDate] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<FormErrors<BookField>>({});
  const [formError, setFormError] = useState<string | null>(null);

  const doctorList = doctors.data ?? [];
  // A single available doctor (a doctor booking for themself, or one assignment) is pre-selected.
  const doctorId = chosenDoctorId || (doctorList.length === 1 ? (doctorList[0]?.id ?? '') : '');

  function changeDoctor(next: string) {
    setChosenDoctorId(next);
    setPatient(null); // a patient belongs to one doctor
    setErrors((current) => ({...current, doctorId: undefined, patientId: undefined}));
  }

  function setQuickEnd(minutes: number) {
    const value = addMinutesToTime(start, minutes);

    if (value) {
      setEnd(value);
      setErrors((current) => ({...current, endTime: undefined}));
    }
  }

  function showErrors(found: FormErrors<BookField>) {
    setErrors(found);
    focusFirstInvalid(FIELD_ORDER.filter((field) => found[field]).map(fieldId));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);

    const input = {
      doctorId,
      patientId: patient?.id ?? '',
      appointmentDate: date,
      startTime: start,
      endTime: end,
      reason
    };
    const found = validateBooking(input, getClinicNow());

    if (hasErrors(found)) {
      showErrors(found);
      return;
    }

    setErrors({});
    mutation.mutate(input, {
      onSuccess: () => router.push('/appointments'),
      onError: (error) => {
        const fieldErrors = isApiError(error) ? mapFieldErrors(error, FIELD_ORDER) : {};

        if (hasErrors(fieldErrors)) {
          showErrors(fieldErrors);
        } else {
          setFormError(errorMessage(error));
        }
      }
    });
  }

  return (
    <div className="grid gap-6">
      <header>
        <h1 className="m-0 text-2xl font-black">{t('booking.title')}</h1>
        <p className="m-0 mt-1 text-sm text-muted">{t('booking.subtitle')}</p>
      </header>

      {identity.status === 'loading' ? <SkeletonRows rows={4} label={t('states.loading')} /> : null}
      {identity.status === 'none' ? <IdentityRequired /> : null}

      {ready ? (
        <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
          <form
            onSubmit={handleSubmit}
            noValidate
            aria-label={t('booking.title')}
            className="grid gap-5 rounded-2xl border border-border bg-surface p-5"
          >
            {formError ? <Alert tone="error">{formError}</Alert> : null}

            {doctors.isError ? (
              <Alert
                tone="error"
                action={
                  <Button size="sm" onClick={() => void doctors.refetch()}>
                    {t('actions.retry')}
                  </Button>
                }
              >
                {errorMessage(doctors.error)}
              </Alert>
            ) : null}

            {doctors.isSuccess && doctorList.length === 0 ? (
              <Alert tone="warning" title={t('booking.noDoctorsTitle')}>
                {t('booking.noDoctorsHint')}
              </Alert>
            ) : null}

            <Field id={fieldId('doctorId')} label={t('fields.doctor')} required error={message(errors.doctorId)}>
              <select
                value={doctorId}
                disabled={doctors.isPending}
                onChange={(event) => changeDoctor(event.target.value)}
                className={controlClass}
                {...fieldAria(fieldId('doctorId'), message(errors.doctorId))}
              >
                <option value="">{t('booking.doctorPlaceholder')}</option>
                {doctorList.map((doctor) => (
                  <option key={doctor.id} value={doctor.id}>
                    {doctor.fullName}
                    {doctor.specialization ? ` — ${doctor.specialization}` : ''}
                  </option>
                ))}
              </select>
            </Field>

            <Field id={fieldId('patientId')} label={t('fields.patient')} required error={message(errors.patientId)}>
              <PatientCombobox
                id={fieldId('patientId')}
                doctorId={doctorId}
                value={patient}
                onChange={(next) => {
                  setPatient(next);
                  setErrors((current) => ({...current, patientId: undefined}));
                }}
                error={message(errors.patientId)}
              />
            </Field>

            <Field id={fieldId('appointmentDate')} label={t('fields.date')} required error={message(errors.appointmentDate)}>
              <input
                type="date"
                value={date}
                min={getClinicNow().date}
                onChange={(event) => setDate(event.target.value)}
                className={controlClass}
                {...fieldAria(fieldId('appointmentDate'), message(errors.appointmentDate))}
              />
            </Field>

            <div className="grid gap-5 sm:grid-cols-2">
              <Field id={fieldId('startTime')} label={t('fields.startTime')} required error={message(errors.startTime)}>
                <input
                  type="time"
                  value={start}
                  onChange={(event) => setStart(event.target.value)}
                  className={controlClass}
                  {...fieldAria(fieldId('startTime'), message(errors.startTime))}
                />
              </Field>
              <Field id={fieldId('endTime')} label={t('fields.endTime')} required error={message(errors.endTime)}>
                <input
                  type="time"
                  value={end}
                  onChange={(event) => setEnd(event.target.value)}
                  className={controlClass}
                  {...fieldAria(fieldId('endTime'), message(errors.endTime))}
                />
              </Field>
            </div>

            <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t('booking.quickEnd')}>
              <span className="text-xs text-muted">{t('booking.quickEnd')}</span>
              {QUICK_DURATIONS.map((minutes) => (
                <Button
                  key={minutes}
                  size="sm"
                  disabled={addMinutesToTime(start, minutes) === null}
                  onClick={() => setQuickEnd(minutes)}
                >
                  {t('booking.minutes', {minutes})}
                </Button>
              ))}
            </div>

            <Field
              id={fieldId('reason')}
              label={t('fields.reason')}
              error={message(errors.reason)}
              aside={t('booking.reasonCounter', {count: reason.trim().length, max: REASON_MAX_LENGTH})}
            >
              <textarea
                value={reason}
                rows={3}
                onChange={(event) => setReason(event.target.value)}
                className={`${controlClass} py-2`}
                {...fieldAria(fieldId('reason'), message(errors.reason))}
              />
            </Field>

            <div className="flex flex-wrap justify-end gap-2">
              <Link
                href="/appointments"
                className="inline-flex min-h-10 items-center justify-center rounded-full border border-border px-5 text-sm font-bold text-foreground transition hover:border-brand hover:text-brand"
              >
                {t('actions.back')}
              </Link>
              <Button type="submit" variant="primary" loading={mutation.isPending}>
                {t('booking.submit')}
              </Button>
            </div>
          </form>

          <DaySchedulePanel doctorId={doctorId} date={date} start={start} end={end} />
        </div>
      ) : null}
    </div>
  );
}
