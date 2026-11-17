'use client';

import {useEffect, useRef, useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {Link, useRouter} from '@/i18n/navigation';
import {useCreatePatient, useDoctorOptions, usePatientActor, useUpdatePatient} from '../hooks/usePatients';
import {getPatientErrorKey, serverFieldNames} from '../services/patient-errors';
import {
  BLOOD_TYPES,
  GENDERS,
  PATIENT_LIMITS,
  type Patient,
  type SavePatientInput
} from '../types/patient.types';
import {
  EMPTY_PATIENT_VALUES,
  PATIENT_FIELD_ORDER,
  blankToNull,
  todayIso,
  validatePatientForm,
  type PatientField,
  type PatientFormErrors,
  type PatientFormValues
} from '../validation/patient.validation';
import {Alert, Button, Field, controlClass, fieldAria} from './ui';

type PatientFormProps = {patient?: Patient};

const fieldId = (field: PatientField) => `patient-${field}`;

/**
 * Create / edit form (F8). Role-aware (Q-18): a doctor sees and sends the blood type; a secretary
 * never sees the field and never sends it, and must pick one of their assigned doctors when
 * creating. Client validation is UX only; server field errors are mapped back onto the fields.
 */
export function PatientForm({patient}: PatientFormProps) {
  const isEdit = patient !== undefined;
  const t = useTranslations('Patients.form');
  const validationT = useTranslations('Patients.validation');
  const errorsT = useTranslations('Patients.errors');
  const genderT = useTranslations('Patients.gender');
  const bloodT = useTranslations('Patients.bloodType');
  const router = useRouter();
  const {role} = usePatientActor();
  const isDoctor = role === 'Doctor';
  const needsDoctor = role === 'Secretary' && !isEdit;

  const createMutation = useCreatePatient();
  const updateMutation = useUpdatePatient(patient?.id ?? '');
  const mutation = isEdit ? updateMutation : createMutation;
  const doctors = useDoctorOptions(needsDoctor);

  const [entered, setValues] = useState<PatientFormValues>(() => initialValues(patient));
  const [errors, setErrors] = useState<PatientFormErrors>({});
  const submitted = useRef(false);
  const focusTarget = useRef<PatientField | null>(null);

  // A secretary with a single assigned doctor does not have to choose.
  const onlyDoctorId = doctors.data?.length === 1 ? doctors.data[0]?.id : undefined;
  const values: PatientFormValues = {...entered, doctorId: entered.doctorId || onlyDoctorId || ''};

  useEffect(() => {
    const target = focusTarget.current;

    if (target) {
      focusTarget.current = null;
      document.getElementById(fieldId(target))?.focus();
    }
  }, [errors]);

  const serverFields = serverFieldNames(mutation.error);

  function setField<K extends PatientField>(field: K, value: PatientFormValues[K]) {
    const next = {...values, [field]: value};

    setValues(next);

    if (submitted.current) {
      setErrors(validate(next));
    }

    if (mutation.error) {
      mutation.reset();
    }
  }

  function validate(candidate: PatientFormValues): PatientFormErrors {
    const found = validatePatientForm(candidate, {requireDoctor: needsDoctor, today: todayIso()});

    // A secretary has no blood-type control, so it can never be invalid for them.
    if (!isDoctor) {
      delete found.bloodType;
    }

    return found;
  }

  function errorFor(field: PatientField): string | null {
    const key = errors[field];

    if (key) {
      return validationT(key);
    }

    return serverFields.has(field) ? validationT('invalid') : null;
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    submitted.current = true;

    const found = validate(values);
    const firstInvalid = PATIENT_FIELD_ORDER.find((field) => found[field]);

    focusTarget.current = firstInvalid ?? null;
    setErrors(found);

    if (firstInvalid) {
      return;
    }

    const input: SavePatientInput = {
      ...(needsDoctor ? {doctorId: values.doctorId} : {}),
      fullName: values.fullName.trim(),
      dateOfBirth: values.dateOfBirth,
      gender: values.gender as SavePatientInput['gender'],
      phone: blankToNull(values.phone),
      address: blankToNull(values.address),
      emergencyContact: blankToNull(values.emergencyContact),
      // Medical field: only a doctor's request carries it (Q-18).
      ...(isDoctor ? {bloodType: values.bloodType === '' ? null : values.bloodType} : {})
    };

    mutation.mutate(input, {onSuccess: () => router.push('/patients')});
  }

  const hasClientErrors = Object.keys(errors).length > 0;

  return (
    <form onSubmit={handleSubmit} noValidate className="grid gap-5">
      {mutation.isError ? <Alert tone="error">{errorsT(getPatientErrorKey(mutation.error))}</Alert> : null}
      {!mutation.isError && hasClientErrors ? <Alert tone="error">{t('validationSummary')}</Alert> : null}
      {!isDoctor ? <Alert>{t('administrativeOnly')}</Alert> : null}

      <fieldset disabled={mutation.isPending} className="grid gap-4 border-0 p-0 sm:grid-cols-2">
        {needsDoctor ? (
          <div className="sm:col-span-2">
            <Field id={fieldId('doctorId')} label={t('fields.doctor')} required error={errorFor('doctorId')}>
              <select
                {...fieldAria(fieldId('doctorId'), errorFor('doctorId'))}
                value={values.doctorId}
                onChange={(event) => setField('doctorId', event.target.value)}
                className={controlClass}
              >
                <option value="">{doctors.isLoading ? t('loadingDoctors') : t('doctorPlaceholder')}</option>
                {(doctors.data ?? []).map((doctor) => (
                  <option key={doctor.id} value={doctor.id}>
                    {doctor.fullName}
                  </option>
                ))}
              </select>
            </Field>
          </div>
        ) : null}

        <div className="sm:col-span-2">
          <Field id={fieldId('fullName')} label={t('fields.fullName')} required error={errorFor('fullName')}>
            <input
              {...fieldAria(fieldId('fullName'), errorFor('fullName'))}
              type="text"
              dir="auto"
              autoComplete="off"
              maxLength={PATIENT_LIMITS.fullName + 50}
              value={values.fullName}
              onChange={(event) => setField('fullName', event.target.value)}
              className={controlClass}
            />
          </Field>
        </div>

        <Field id={fieldId('dateOfBirth')} label={t('fields.dateOfBirth')} required error={errorFor('dateOfBirth')}>
          <input
            {...fieldAria(fieldId('dateOfBirth'), errorFor('dateOfBirth'))}
            type="date"
            dir="ltr"
            max={todayIso()}
            value={values.dateOfBirth}
            onChange={(event) => setField('dateOfBirth', event.target.value)}
            className={controlClass}
          />
        </Field>

        <Field id={fieldId('gender')} label={t('fields.gender')} required error={errorFor('gender')}>
          <select
            {...fieldAria(fieldId('gender'), errorFor('gender'))}
            value={values.gender}
            onChange={(event) => setField('gender', event.target.value as PatientFormValues['gender'])}
            className={controlClass}
          >
            <option value="">{t('genderPlaceholder')}</option>
            {GENDERS.map((gender) => (
              <option key={gender} value={gender}>
                {genderT(gender)}
              </option>
            ))}
          </select>
        </Field>

        <Field id={fieldId('phone')} label={t('fields.phone')} error={errorFor('phone')}>
          <input
            {...fieldAria(fieldId('phone'), errorFor('phone'))}
            type="tel"
            dir="ltr"
            autoComplete="off"
            value={values.phone}
            onChange={(event) => setField('phone', event.target.value)}
            className={controlClass}
          />
        </Field>

        <Field
          id={fieldId('emergencyContact')}
          label={t('fields.emergencyContact')}
          error={errorFor('emergencyContact')}
        >
          <input
            {...fieldAria(fieldId('emergencyContact'), errorFor('emergencyContact'))}
            type="text"
            dir="auto"
            autoComplete="off"
            value={values.emergencyContact}
            onChange={(event) => setField('emergencyContact', event.target.value)}
            className={controlClass}
          />
        </Field>

        <div className="sm:col-span-2">
          <Field id={fieldId('address')} label={t('fields.address')} error={errorFor('address')}>
            <input
              {...fieldAria(fieldId('address'), errorFor('address'))}
              type="text"
              dir="auto"
              autoComplete="off"
              value={values.address}
              onChange={(event) => setField('address', event.target.value)}
              className={controlClass}
            />
          </Field>
        </div>

        {isDoctor ? (
          <Field
            id={fieldId('bloodType')}
            label={t('fields.bloodType')}
            hint={t('bloodTypeHint')}
            error={errorFor('bloodType')}
          >
            <select
              {...fieldAria(fieldId('bloodType'), errorFor('bloodType'))}
              value={values.bloodType}
              onChange={(event) => setField('bloodType', event.target.value as PatientFormValues['bloodType'])}
              className={controlClass}
            >
              <option value="">{t('bloodTypeNone')}</option>
              {BLOOD_TYPES.map((type) => (
                <option key={type} value={type}>
                  {bloodT(type)}
                </option>
              ))}
            </select>
          </Field>
        ) : null}
      </fieldset>

      <div className="flex flex-wrap gap-3">
        <Button type="submit" variant="primary" loading={mutation.isPending}>
          {mutation.isPending ? t('saving') : t('save')}
        </Button>
        <Link
          href="/patients"
          className="inline-flex min-h-10 items-center rounded-full border border-border px-5 text-sm font-bold text-foreground transition hover:border-brand hover:text-brand"
        >
          {t('cancel')}
        </Link>
      </div>
    </form>
  );
}

function initialValues(patient: Patient | undefined): PatientFormValues {
  if (!patient) {
    return EMPTY_PATIENT_VALUES;
  }

  return {
    doctorId: patient.doctorId ?? '',
    fullName: patient.fullName,
    dateOfBirth: patient.dateOfBirth,
    gender: patient.gender,
    phone: patient.phone ?? '',
    address: patient.address ?? '',
    emergencyContact: patient.emergencyContact ?? '',
    bloodType: patient.bloodType ?? ''
  };
}
