'use client';

import {useEffect, useRef, useState, type FormEvent} from 'react';
import {FiSave, FiX} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {fieldErrorsOf} from '../errors';
import {
  useCreateMedicationMutation,
  useUpdateMedicationMutation
} from '../hooks/use-medication-mutations';
import type {CreateMedicationRequest, MedicationResponse} from '../types';
import {readDevToken, useDevToken} from '../use-dev-token';
import {
  MEDICATION_FIELD_IDS,
  MEDICATION_FIELD_LIMITS,
  MEDICATION_FIELD_ORDER,
  trimToNull,
  validateMedicationForm,
  type MedicationField,
  type MedicationFormErrors,
  type MedicationFormValues
} from '../validation';
import {FormField} from './FormField';
import {MutationAlert} from './MutationAlert';
import {
  controlClassName,
  primaryButtonClassName,
  secondaryButtonClassName,
  textareaClassName
} from './form-styles';

type MedicationFormBaseProps = {
  visitId: string;
  onDone: () => void;
  onCancel: () => void;
};

type MedicationFormProps =
  | (MedicationFormBaseProps & {mode: 'create'})
  | (MedicationFormBaseProps & {mode: 'edit'; medication: MedicationResponse});

/** Add/edit medication line, with the same validation and focus contract as the other forms. */
export function MedicationForm(props: MedicationFormProps) {
  const isCreate = props.mode === 'create';
  const t = useTranslations('MedicalRecords.medication');
  const validationT = useTranslations('MedicalRecords.validation');
  const commonT = useTranslations('MedicalRecords.common');

  const {token, isReady} = useDevToken();
  const createMutation = useCreateMedicationMutation(props.visitId);
  const updateMutation = useUpdateMedicationMutation(props.visitId);

  const [values, setValues] = useState<MedicationFormValues>(() =>
    initialValues(props)
  );
  const [errors, setErrors] = useState<MedicationFormErrors>({});
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

    const firstField = MEDICATION_FIELD_ORDER.find(
      (field) => (serverFieldErrors[field]?.length ?? 0) > 0
    );

    if (!firstField) {
      return;
    }

    const element = document.getElementById(MEDICATION_FIELD_IDS[firstField]);

    if (element instanceof HTMLElement) {
      element.focus();
    }
  }, [activeError, serverFieldErrors]);

  function setField(field: MedicationField, value: string) {
    const next: MedicationFormValues = {...values, [field]: value};
    setValues(next);

    if (submittedRef.current) {
      setErrors(validateMedicationForm(next));
    }

    if (activeError) {
      if (isCreate) {
        createMutation.reset();
      } else {
        updateMutation.reset();
      }
    }
  }

  function clientError(field: MedicationField): string | null {
    const message = errors[field];

    if (!message) {
      return null;
    }

    return message.values
      ? validationT(message.key, message.values)
      : validationT(message.key);
  }

  function serverError(field: MedicationField): string | null {
    const messages = serverFieldErrors[field];

    return messages && messages.length > 0 ? messages.join(' ') : null;
  }

  function errorFor(field: MedicationField): string | null {
    return clientError(field) ?? serverError(field);
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    submittedRef.current = true;

    const nextErrors = validateMedicationForm(values);
    const firstInvalid = MEDICATION_FIELD_ORDER.find((field) => nextErrors[field]);

    focusTargetRef.current = firstInvalid
      ? MEDICATION_FIELD_IDS[firstInvalid]
      : null;
    setErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) {
      return;
    }

    const currentToken = readDevToken();

    if (!currentToken) {
      return;
    }

    const payload: CreateMedicationRequest = {
      medicationName: values.medicationName.trim(),
      dosage: values.dosage.trim(),
      frequency: values.frequency.trim(),
      duration: trimToNull(values.duration),
      instructions: trimToNull(values.instructions)
    };

    if (props.mode === 'edit') {
      updateMutation.mutate(
        {medicationId: props.medication.id, payload, token: currentToken},
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
        <FormField
          id={MEDICATION_FIELD_IDS.medicationName}
          label={t('nameLabel')}
          error={errorFor('medicationName')}
          required
        >
          {(control) => (
            <input
              {...control}
              type="text"
              autoComplete="off"
              maxLength={MEDICATION_FIELD_LIMITS.medicationName}
              value={values.medicationName}
              onChange={(event) => setField('medicationName', event.target.value)}
              className={controlClassName}
            />
          )}
        </FormField>

        <div className="grid gap-4 sm:grid-cols-3">
          <FormField
            id={MEDICATION_FIELD_IDS.dosage}
            label={t('dosageLabel')}
            error={errorFor('dosage')}
            required
          >
            {(control) => (
              <input
                {...control}
                type="text"
                autoComplete="off"
                maxLength={MEDICATION_FIELD_LIMITS.dosage}
                value={values.dosage}
                onChange={(event) => setField('dosage', event.target.value)}
                className={controlClassName}
              />
            )}
          </FormField>

          <FormField
            id={MEDICATION_FIELD_IDS.frequency}
            label={t('frequencyLabel')}
            error={errorFor('frequency')}
            required
          >
            {(control) => (
              <input
                {...control}
                type="text"
                autoComplete="off"
                maxLength={MEDICATION_FIELD_LIMITS.frequency}
                value={values.frequency}
                onChange={(event) => setField('frequency', event.target.value)}
                className={controlClassName}
              />
            )}
          </FormField>

          <FormField
            id={MEDICATION_FIELD_IDS.duration}
            label={t('durationLabel')}
            error={errorFor('duration')}
          >
            {(control) => (
              <input
                {...control}
                type="text"
                autoComplete="off"
                maxLength={MEDICATION_FIELD_LIMITS.duration}
                value={values.duration}
                onChange={(event) => setField('duration', event.target.value)}
                className={controlClassName}
              />
            )}
          </FormField>
        </div>

        <FormField
          id={MEDICATION_FIELD_IDS.instructions}
          label={t('instructionsLabel')}
          error={errorFor('instructions')}
        >
          {(control) => (
            <textarea
              {...control}
              rows={3}
              maxLength={MEDICATION_FIELD_LIMITS.instructions}
              value={values.instructions}
              onChange={(event) => setField('instructions', event.target.value)}
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

function initialValues(props: MedicationFormProps): MedicationFormValues {
  if (props.mode === 'edit') {
    return {
      medicationName: props.medication.medicationName,
      dosage: props.medication.dosage,
      frequency: props.medication.frequency,
      duration: props.medication.duration ?? '',
      instructions: props.medication.instructions ?? ''
    };
  }

  return {
    medicationName: '',
    dosage: '',
    frequency: '',
    duration: '',
    instructions: ''
  };
}
