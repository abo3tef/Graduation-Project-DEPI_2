'use client';

import {useEffect, useId, useRef, useState, type FormEvent} from 'react';
import {useTranslations} from 'next-intl';
import {fieldErrorsOf} from '../errors';
import {readCredentialScope} from '../credential-scope';
import {useCreateVitalSignsMutation, useUpdateVitalSignsMutation} from '../hooks/use-vital-signs-mutations';
import {readMedicalToken, useMedicalCredentials} from '../use-medical-credentials';
import type {VitalSignsResponse} from '../types';
import {
  VITAL_SIGNS_FIELDS, VITAL_SIGNS_LIMITS, initialVitalSignsValues, toVitalSignsRequest,
  validateVitalSignsForm, type VitalSignsField, type VitalSignsFormErrors
} from '../vital-signs-validation';
import {FormField} from './FormField';
import {MutationAlert} from './MutationAlert';
import {controlClassName, primaryButtonClassName, secondaryButtonClassName} from './form-styles';

type BaseProps = {visitId: string; onDone: () => void; onCancel: () => void};
type VitalSignsFormProps = BaseProps & (
  {mode: 'create'} | {mode: 'edit'; vitalSigns: VitalSignsResponse}
);

/** Host only after permitted doctor/owner state is established. This component adds no route/control. */
export function VitalSignsForm(props: VitalSignsFormProps) {
  const t = useTranslations('MedicalRecords.vitalSigns');
  const commonT = useTranslations('MedicalRecords.common');
  const identity = useMedicalCredentials();
  const [openingScope] = useState(identity.credentialScope);
  const [values, setValues] = useState(() => initialVitalSignsValues(props.mode === 'edit' ? props.vitalSigns : undefined));
  const [errors, setErrors] = useState<VitalSignsFormErrors>({});
  const [saved, setSaved] = useState(false);
  const lock = useRef(false);
  const prefix = useId();
  const messages = {success: t('saved'), error: t('saveFailed')};
  const create = useCreateVitalSignsMutation(props.visitId, messages);
  const update = useUpdateVitalSignsMutation(props.visitId, messages);
  const active = props.mode === 'create' ? create : update;
  const serverErrors = fieldErrorsOf(active.error);
  const fieldId = (field: VitalSignsField) => `${prefix}-${field}`;

  useEffect(() => {
    if (!active.error) return;
    const first = VITAL_SIGNS_FIELDS.find(field => (fieldErrorsOf(active.error)[field]?.length ?? 0) > 0);
    if (first) document.getElementById(`${prefix}-${first}`)?.focus();
  }, [active.error, prefix]);

  if (!identity.isReady || !identity.token || openingScope !== identity.credentialScope) return null;

  function setField(field: VitalSignsField, value: string) {
    setValues(previous => ({...previous, [field]: value}));
    setErrors({});
    setSaved(false);
    active.reset();
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (lock.current || active.isPending || openingScope !== readCredentialScope()) return;
    const nextErrors = validateVitalSignsForm(values);
    setErrors(nextErrors);
    const first = VITAL_SIGNS_FIELDS.find(field => nextErrors[field]);
    if (first || nextErrors.form) {
      document.getElementById(fieldId(first ?? VITAL_SIGNS_FIELDS[0]))?.focus();
      return;
    }
    const token = readMedicalToken();
    if (!token || token !== identity.token) return;
    lock.current = true;
    setSaved(false);
    const options = {
      onSuccess: () => {
        if (openingScope !== readCredentialScope()) return;
        setSaved(true);
        props.onDone();
      },
      onSettled: () => {lock.current = false;}
    };
    const payload = toVitalSignsRequest(values);
    if (props.mode === 'edit') {
      update.mutate({vitalSignsId: props.vitalSigns.id, payload, token}, options);
    } else {
      create.mutate({payload, token}, options);
    }
  }

  return (
    <form onSubmit={submit} noValidate aria-busy={active.isPending}
      aria-describedby={errors.form ? `${prefix}-summary` : undefined}
      className="flex flex-col gap-4 rounded-xl border border-brand/30 bg-background p-4">
      <h3 className="text-base font-bold">{t(props.mode === 'create' ? 'createTitle' : 'editTitle')}</h3>
      <MutationAlert error={active.error} />
      {errors.form ? <p id={`${prefix}-summary`} role="alert" className="text-sm text-danger">{t(`validation.${errors.form.key}`)}</p> : null}
      {saved ? <p role="status">{t('saved')}</p> : null}
      <fieldset disabled={active.isPending} className="grid gap-4 border-0 p-0 sm:grid-cols-2">
        {VITAL_SIGNS_FIELDS.map(field => {
          const descriptor = errors[field];
          const error = descriptor ? t(`validation.${descriptor.key}`, descriptor.values)
            : serverErrors[field]?.length ? t('validation.serverRejected') : undefined;
          const limits = field === 'bloodPressure' ? undefined : VITAL_SIGNS_LIMITS[field];
          return (
            <FormField key={field} id={fieldId(field)} label={t(`fields.${field}`)} hint={t(`units.${field}`)} error={error}>
              {control => <input {...control} type="text" dir="ltr" autoComplete="off"
                inputMode={field === 'bloodPressure' ? 'text' : limits?.integer ? 'numeric' : 'decimal'}
                maxLength={field === 'bloodPressure' ? VITAL_SIGNS_LIMITS.bloodPressure.maxLength : undefined}
                value={values[field]} onChange={event => setField(field, event.target.value)} className={controlClassName} />}
            </FormField>
          );
        })}
        <div className="flex flex-wrap gap-2 sm:col-span-2">
          <button type="submit" disabled={active.isPending} className={primaryButtonClassName}>
            {t(active.isPending ? 'saving' : props.mode === 'create' ? 'createSubmit' : 'editSubmit')}
          </button>
          <button type="button" onClick={props.onCancel} disabled={active.isPending} className={secondaryButtonClassName}>
            {commonT('cancel')}
          </button>
        </div>
      </fieldset>
    </form>
  );
}
