'use client';

import {useEffect, useRef, useState, type FormEvent} from 'react';
import {FiSave, FiX} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {fieldErrorsOf} from '../errors';
import {
  useCreateVisitMutation,
  useUpdateVisitMutation
} from '../hooks/use-visit-mutations';
import type {MedicalVisitResponse} from '../types';
import {readDevToken, useDevToken} from '../use-dev-token';
import {
  VISIT_FIELD_IDS,
  VISIT_FIELD_LIMITS,
  VISIT_FIELD_ORDER,
  toDateTimeLocalValue,
  toVisitDateIso,
  trimToNull,
  validateVisitForm,
  type VisitField,
  type VisitFormErrors,
  type VisitFormValues
} from '../validation';
import {FormField} from './FormField';
import {MutationAlert} from './MutationAlert';
import {
  controlClassName,
  primaryButtonClassName,
  secondaryButtonClassName,
  textareaClassName
} from './form-styles';

type VisitFormProps =
  | {
      mode: 'create';
      initialPatientId?: string;
      onSuccess: (visit: MedicalVisitResponse) => void;
      onCancel?: () => void;
    }
  | {
      mode: 'edit';
      initialVisit: MedicalVisitResponse;
      onSuccess: (visit: MedicalVisitResponse) => void;
      onCancel: () => void;
    };

/**
 * Shared create/edit form for a visit. React state only (D-29); client validation is UX and
 * the server `ValidationProblemDetails.errors` are mapped back onto the same fields.
 */
export function VisitForm(props: VisitFormProps) {
  const {mode, onSuccess, onCancel} = props;
  const isCreate = mode === 'create';

  const t = useTranslations('MedicalRecords.visitForm');
  const validationT = useTranslations('MedicalRecords.validation');
  const commonT = useTranslations('MedicalRecords.common');

  const {token, isReady} = useDevToken();
  const createMutation = useCreateVisitMutation();
  const updateMutation = useUpdateVisitMutation(
    isCreate ? '' : props.initialVisit.id
  );

  const [values, setValues] = useState<VisitFormValues>(() => initialValues(props));
  const [errors, setErrors] = useState<VisitFormErrors>({});
  const submittedRef = useRef(false);
  const focusTargetRef = useRef<string | null>(null);

  const activeError = isCreate ? createMutation.error : updateMutation.error;
  const isPending = isCreate ? createMutation.isPending : updateMutation.isPending;
  const serverFieldErrors = fieldErrorsOf(activeError);

  // Move focus to the first invalid control after a client-side submit.
  useEffect(() => {
    const target = focusTargetRef.current;

    if (!target) {
      return;
    }

    focusTargetRef.current = null;
    const element = document.getElementById(target);

    if (element instanceof HTMLElement) {
      element.focus();
    }
  }, [errors]);

  // Move focus to the first field the server rejected.
  useEffect(() => {
    if (!activeError) {
      return;
    }

    const firstField = VISIT_FIELD_ORDER.find(
      (field) => (serverFieldErrors[field]?.length ?? 0) > 0
    );

    if (!firstField) {
      return;
    }

    const element = document.getElementById(VISIT_FIELD_IDS[firstField]);

    if (element instanceof HTMLElement) {
      element.focus();
    }
  }, [activeError, serverFieldErrors]);

  function setField(field: VisitField, value: string) {
    const next: VisitFormValues = {...values, [field]: value};
    setValues(next);

    if (submittedRef.current) {
      setErrors(validateVisitForm(next, mode));
    }

    if (activeError) {
      if (isCreate) {
        createMutation.reset();
      } else {
        updateMutation.reset();
      }
    }
  }

  function clientError(field: VisitField): string | null {
    const message = errors[field];

    if (!message) {
      return null;
    }

    return message.values
      ? validationT(message.key, message.values)
      : validationT(message.key);
  }

  function serverError(field: VisitField): string | null {
    const messages = serverFieldErrors[field];

    return messages && messages.length > 0 ? messages.join(' ') : null;
  }

  function errorFor(field: VisitField): string | null {
    return clientError(field) ?? serverError(field);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    submittedRef.current = true;

    const nextErrors = validateVisitForm(values, mode);
    const firstInvalid = VISIT_FIELD_ORDER.find((field) => nextErrors[field]);

    focusTargetRef.current = firstInvalid ? VISIT_FIELD_IDS[firstInvalid] : null;
    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    const currentToken = readDevToken();

    if (!currentToken) {
      return;
    }

    if (isCreate) {
      createMutation.mutate(
        {
          payload: {
            patientId: values.patientId.trim(),
            appointmentId: trimToNull(values.appointmentId),
            visitDate: toVisitDateIso(values.visitDate),
            chiefComplaint: trimToNull(values.chiefComplaint),
            symptoms: trimToNull(values.symptoms),
            clinicalNotes: trimToNull(values.clinicalNotes)
          },
          token: currentToken
        },
        {onSuccess}
      );

      return;
    }

    updateMutation.mutate(
      {
        payload: {
          visitDate: toVisitDateIso(values.visitDate),
          chiefComplaint: trimToNull(values.chiefComplaint),
          symptoms: trimToNull(values.symptoms),
          clinicalNotes: trimToNull(values.clinicalNotes)
        },
        token: currentToken
      },
      {onSuccess}
    );
  }

  const hasClientErrors = Object.keys(errors).length > 0;

  return (
    <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-5">
      <MutationAlert error={activeError} />

      {!activeError && hasClientErrors ? (
        <p
          role="alert"
          className="rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm font-medium text-danger"
        >
          {commonT('validationSummary')}
        </p>
      ) : null}

      {!isCreate ? (
        <p className="rounded-xl border border-border bg-surface-2 px-4 py-3 text-xs text-muted">
          {t('readOnlyNote')}
        </p>
      ) : null}

      <fieldset
        disabled={isPending}
        className="flex flex-col gap-5 border-0 p-0"
      >
        {isCreate ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              id={VISIT_FIELD_IDS.patientId}
              label={t('patientId')}
              hint={t('patientIdHint')}
              error={errorFor('patientId')}
              required
            >
              {(control) => (
                <input
                  {...control}
                  type="text"
                  dir="ltr"
                  autoComplete="off"
                  spellCheck={false}
                  value={values.patientId}
                  onChange={(event) => setField('patientId', event.target.value)}
                  className={`${controlClassName} font-mono`}
                />
              )}
            </FormField>

            <FormField
              id={VISIT_FIELD_IDS.appointmentId}
              label={t('appointmentId')}
              hint={t('appointmentIdHint')}
              error={errorFor('appointmentId')}
            >
              {(control) => (
                <input
                  {...control}
                  type="text"
                  dir="ltr"
                  autoComplete="off"
                  spellCheck={false}
                  value={values.appointmentId}
                  onChange={(event) =>
                    setField('appointmentId', event.target.value)
                  }
                  className={`${controlClassName} font-mono`}
                />
              )}
            </FormField>
          </div>
        ) : null}

        <FormField
          id={VISIT_FIELD_IDS.visitDate}
          label={t('visitDate')}
          error={errorFor('visitDate')}
          required
        >
          {(control) => (
            <input
              {...control}
              type="datetime-local"
              value={values.visitDate}
              onChange={(event) => setField('visitDate', event.target.value)}
              className={controlClassName}
            />
          )}
        </FormField>

        <FormField
          id={VISIT_FIELD_IDS.chiefComplaint}
          label={t('chiefComplaint')}
          error={errorFor('chiefComplaint')}
        >
          {(control) => (
            <textarea
              {...control}
              rows={3}
              maxLength={VISIT_FIELD_LIMITS.chiefComplaint}
              value={values.chiefComplaint}
              onChange={(event) =>
                setField('chiefComplaint', event.target.value)
              }
              className={textareaClassName}
            />
          )}
        </FormField>

        <FormField
          id={VISIT_FIELD_IDS.symptoms}
          label={t('symptoms')}
          error={errorFor('symptoms')}
        >
          {(control) => (
            <textarea
              {...control}
              rows={3}
              maxLength={VISIT_FIELD_LIMITS.symptoms}
              value={values.symptoms}
              onChange={(event) => setField('symptoms', event.target.value)}
              className={textareaClassName}
            />
          )}
        </FormField>

        <FormField
          id={VISIT_FIELD_IDS.clinicalNotes}
          label={t('clinicalNotes')}
          error={errorFor('clinicalNotes')}
        >
          {(control) => (
            <textarea
              {...control}
              rows={5}
              maxLength={VISIT_FIELD_LIMITS.clinicalNotes}
              value={values.clinicalNotes}
              onChange={(event) => setField('clinicalNotes', event.target.value)}
              className={textareaClassName}
            />
          )}
        </FormField>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="submit"
            disabled={isPending || !isReady || !token}
            className={primaryButtonClassName}
          >
            <FiSave aria-hidden className="size-4" />
            {isPending
              ? t('submitting')
              : isCreate
                ? t('submitCreate')
                : t('submitEdit')}
          </button>

          {onCancel ? (
            <button
              type="button"
              onClick={onCancel}
              disabled={isPending}
              className={secondaryButtonClassName}
            >
              <FiX aria-hidden className="size-4" />
              {commonT('cancel')}
            </button>
          ) : null}
        </div>
      </fieldset>
    </form>
  );
}

function initialValues(props: VisitFormProps): VisitFormValues {
  if (props.mode === 'edit') {
    const visit = props.initialVisit;

    return {
      patientId: visit.patientId,
      appointmentId: visit.appointmentId ?? '',
      visitDate: toDateTimeLocalValue(visit.visitDate),
      chiefComplaint: visit.chiefComplaint ?? '',
      symptoms: visit.symptoms ?? '',
      clinicalNotes: visit.clinicalNotes ?? ''
    };
  }

  return {
    patientId: props.initialPatientId?.trim() ?? '',
    appointmentId: '',
    visitDate: toDateTimeLocalValue(new Date().toISOString()),
    chiefComplaint: '',
    symptoms: '',
    clinicalNotes: ''
  };
}
