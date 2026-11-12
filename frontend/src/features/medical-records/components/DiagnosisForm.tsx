'use client';

import {useEffect, useRef, useState, type FormEvent} from 'react';
import {FiSave, FiX} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {fieldErrorsOf} from '../errors';
import {
  useCreateDiagnosisMutation,
  useUpdateDiagnosisMutation
} from '../hooks/use-diagnosis-mutations';
import type {CreateDiagnosisRequest, DiagnosisResponse} from '../types';
import {readDevToken, useDevToken} from '../use-dev-token';
import {
  DIAGNOSIS_FIELD_IDS,
  DIAGNOSIS_FIELD_LIMITS,
  DIAGNOSIS_FIELD_ORDER,
  latestDiagnosisDateOnly,
  toDateOnlyValue,
  todayDateOnly,
  trimToNull,
  validateDiagnosisForm,
  type DiagnosisField,
  type DiagnosisFormErrors,
  type DiagnosisFormValues
} from '../validation';
import {FormField} from './FormField';
import {MutationAlert} from './MutationAlert';
import {
  controlClassName,
  primaryButtonClassName,
  secondaryButtonClassName,
  textareaClassName
} from './form-styles';

type DiagnosisFormBaseProps = {
  visitId: string;
  onDone: () => void;
  onCancel: () => void;
};

type DiagnosisFormProps =
  | (DiagnosisFormBaseProps & {mode: 'create'})
  | (DiagnosisFormBaseProps & {mode: 'edit'; diagnosis: DiagnosisResponse});

/**
 * Add/edit diagnosis form. Same contract as `VisitForm`: React state only, client validation for
 * UX, server `ValidationProblemDetails.errors` mapped onto the same fields, focus moved to the
 * first rejected control.
 */
export function DiagnosisForm(props: DiagnosisFormProps) {
  const isCreate = props.mode === 'create';
  const t = useTranslations('MedicalRecords.diagnosis');
  const validationT = useTranslations('MedicalRecords.validation');
  const commonT = useTranslations('MedicalRecords.common');

  const {token, isReady} = useDevToken();
  const createMutation = useCreateDiagnosisMutation(props.visitId);
  const updateMutation = useUpdateDiagnosisMutation(props.visitId);

  const [values, setValues] = useState<DiagnosisFormValues>(() =>
    initialValues(props)
  );
  const [errors, setErrors] = useState<DiagnosisFormErrors>({});
  const submittedRef = useRef(false);
  const focusTargetRef = useRef<string | null>(null);

  const activeError = isCreate ? createMutation.error : updateMutation.error;
  const isPending = isCreate ? createMutation.isPending : updateMutation.isPending;
  const serverFieldErrors = fieldErrorsOf(activeError);

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

  useEffect(() => {
    if (!activeError) {
      return;
    }

    const firstField = DIAGNOSIS_FIELD_ORDER.find(
      (field) => (serverFieldErrors[field]?.length ?? 0) > 0
    );

    if (!firstField) {
      return;
    }

    const element = document.getElementById(DIAGNOSIS_FIELD_IDS[firstField]);

    if (element instanceof HTMLElement) {
      element.focus();
    }
  }, [activeError, serverFieldErrors]);

  function setField(field: DiagnosisField, value: string) {
    const next: DiagnosisFormValues = {...values, [field]: value};
    setValues(next);

    if (submittedRef.current) {
      setErrors(validateDiagnosisForm(next));
    }

    if (activeError) {
      if (isCreate) {
        createMutation.reset();
      } else {
        updateMutation.reset();
      }
    }
  }

  function clientError(field: DiagnosisField): string | null {
    const message = errors[field];

    if (!message) {
      return null;
    }

    return message.values
      ? validationT(message.key, message.values)
      : validationT(message.key);
  }

  function serverError(field: DiagnosisField): string | null {
    const messages = serverFieldErrors[field];

    return messages && messages.length > 0 ? messages.join(' ') : null;
  }

  function errorFor(field: DiagnosisField): string | null {
    return clientError(field) ?? serverError(field);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    submittedRef.current = true;

    const nextErrors = validateDiagnosisForm(values);
    const firstInvalid = DIAGNOSIS_FIELD_ORDER.find((field) => nextErrors[field]);

    focusTargetRef.current = firstInvalid
      ? DIAGNOSIS_FIELD_IDS[firstInvalid]
      : null;
    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    const currentToken = readDevToken();

    if (!currentToken) {
      return;
    }

    const payload: CreateDiagnosisRequest = {
      diagnosisName: values.diagnosisName.trim(),
      description: trimToNull(values.description),
      diagnosisDate: values.diagnosisDate.trim()
    };

    if (props.mode === 'edit') {
      updateMutation.mutate(
        {diagnosisId: props.diagnosis.id, payload, token: currentToken},
        {onSuccess: props.onDone}
      );

      return;
    }

    createMutation.mutate(
      {payload, token: currentToken},
      {onSuccess: props.onDone}
    );
  }

  const hasClientErrors = Object.keys(errors).length > 0;

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      className="flex flex-col gap-4 rounded-xl border border-brand/30 bg-background p-4"
    >
      <h3 className="text-base font-bold text-foreground">
        {isCreate ? t('createTitle') : t('editTitle')}
      </h3>

      <MutationAlert error={activeError} />

      {!activeError && hasClientErrors ? (
        <p
          role="alert"
          className="rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm font-medium text-danger"
        >
          {commonT('validationSummary')}
        </p>
      ) : null}

      <fieldset disabled={isPending} className="flex flex-col gap-4 border-0 p-0">
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            id={DIAGNOSIS_FIELD_IDS.diagnosisName}
            label={t('nameLabel')}
            error={errorFor('diagnosisName')}
            required
          >
            {(control) => (
              <input
                {...control}
                type="text"
                autoComplete="off"
                maxLength={DIAGNOSIS_FIELD_LIMITS.diagnosisName}
                value={values.diagnosisName}
                onChange={(event) => setField('diagnosisName', event.target.value)}
                className={controlClassName}
              />
            )}
          </FormField>

          <FormField
            id={DIAGNOSIS_FIELD_IDS.diagnosisDate}
            label={t('dateLabel')}
            error={errorFor('diagnosisDate')}
            required
          >
            {(control) => (
              <input
                {...control}
                type="date"
                max={latestDiagnosisDateOnly()}
                value={values.diagnosisDate}
                onChange={(event) => setField('diagnosisDate', event.target.value)}
                className={controlClassName}
              />
            )}
          </FormField>
        </div>

        <FormField
          id={DIAGNOSIS_FIELD_IDS.description}
          label={t('descriptionLabel')}
          error={errorFor('description')}
        >
          {(control) => (
            <textarea
              {...control}
              rows={3}
              maxLength={DIAGNOSIS_FIELD_LIMITS.description}
              value={values.description}
              onChange={(event) => setField('description', event.target.value)}
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
              ? t('saving')
              : isCreate
                ? t('createSubmit')
                : t('editSubmit')}
          </button>

          <button
            type="button"
            onClick={props.onCancel}
            disabled={isPending}
            className={secondaryButtonClassName}
          >
            <FiX aria-hidden className="size-4" />
            {commonT('cancel')}
          </button>
        </div>
      </fieldset>
    </form>
  );
}

function initialValues(props: DiagnosisFormProps): DiagnosisFormValues {
  if (props.mode === 'edit') {
    return {
      diagnosisName: props.diagnosis.diagnosisName,
      diagnosisDate: toDateOnlyValue(props.diagnosis.diagnosisDate),
      description: props.diagnosis.description ?? ''
    };
  }

  return {
    diagnosisName: '',
    diagnosisDate: todayDateOnly(),
    description: ''
  };
}
