'use client';

import {useEffect, useRef, useState, type FormEvent} from 'react';
import {FiPlus, FiSave, FiTrash2, FiX} from 'react-icons/fi';
import {useQueryClient} from '@tanstack/react-query';
import {useTranslations} from 'next-intl';
import {PatientSelect, usePatient, type PatientOption} from '@/features/patients';
import {readMedicalToken} from '../use-medical-credentials';
import {
  createDiagnosis,
  createMedicalVisit,
  createMedication,
  createVitalSigns
} from '../api';
import {medicalRecordKeys} from '../keys';
import type {
  CreateDiagnosisRequest,
  CreateMedicationRequest,
  CreateMedicalVisitRequest,
  MedicalVisitResponse,
  SaveVitalSignsRequest
} from '../types';
import {
  initialVitalSignsValues,
  toVitalSignsRequest,
  validateVitalSignsForm,
  VITAL_SIGNS_FIELDS,
  type VitalSignsFormValues
} from '../vital-signs-validation';
import {
  isGuid,
  latestDiagnosisDateOnly,
  toDateTimeLocalValue,
  toVisitDateIso,
  trimToNull,
  validateVisitForm,
  VISIT_FIELD_IDS,
  VISIT_FIELD_ORDER,
  type VisitFormValues
} from '../validation';
import {FormField} from './FormField';
import {
  controlClassName,
  primaryButtonClassName,
  secondaryButtonClassName,
  textareaClassName
} from './form-styles';

type UnifiedVisitFormProps = {
  initialPatientId?: string;
  onSuccess: (visit: MedicalVisitResponse) => void;
  onCancel?: () => void;
};

type MedicationDraft = {
  id: number;
  medicationName: string;
  dosage: string;
  frequency: string;
  duration: string;
  instructions: string;
};

type PartialFailures = {
  diagnosis: boolean;
  vitals: boolean;
  medications: number[];
};

const EMPTY_VISIT: VisitFormValues = {
  patientId: '',
  appointmentId: '',
  visitDate: toDateTimeLocalValue(new Date().toISOString()),
  chiefComplaint: '',
  symptoms: '',
  clinicalNotes: ''
};

export function UnifiedVisitForm({
  initialPatientId = '',
  onSuccess,
  onCancel
}: UnifiedVisitFormProps) {
  const t = useTranslations('MedicalRecords.unifiedVisit');
  const visitT = useTranslations('MedicalRecords.visitForm');
  const vitalT = useTranslations('MedicalRecords.vitalSigns');
  const validationT = useTranslations('MedicalRecords.validation');
  const commonT = useTranslations('MedicalRecords.common');
  const queryClient = useQueryClient();

  const [values, setValues] = useState<VisitFormValues>(() => ({
    ...EMPTY_VISIT,
    patientId: isGuid(initialPatientId) ? initialPatientId : ''
  }));
  const [patient, setPatient] = useState<PatientOption | null>(null);
  const prefilledRef = useRef(false);
  const preselected = usePatient(isGuid(initialPatientId) ? initialPatientId : null);

  const [diagnosisName, setDiagnosisName] = useState('');
  const [diagnosisDescription, setDiagnosisDescription] = useState('');
  const [diagnosisDate, setDiagnosisDate] = useState(latestDiagnosisDateOnly());
  const [vitals, setVitals] = useState<VitalSignsFormValues>(() => initialVitalSignsValues());
  const [medications, setMedications] = useState<MedicationDraft[]>([
    {id: 1, medicationName: '', dosage: '', frequency: '', duration: '', instructions: ''}
  ]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSaving, setIsSaving] = useState(false);
  const [savePhase, setSavePhase] = useState('');
  const [createdVisitId, setCreatedVisitId] = useState<string | null>(null);
  const [createdVisit, setCreatedVisit] = useState<MedicalVisitResponse | null>(null);
  const [partialFailures, setPartialFailures] = useState<PartialFailures | null>(null);
  const lock = useRef(false);
  const nextMedicationId = useRef(2);

  useEffect(() => {
    if (preselected.data && !prefilledRef.current) {
      prefilledRef.current = true;
      setPatient({
        id: preselected.data.id,
        fullName: preselected.data.fullName,
        phone: preselected.data.phone
      });
      setValues(current => ({...current, patientId: preselected.data!.id}));
    }
  }, [preselected.data]);

  function setVisitField(field: keyof VisitFormValues, value: string) {
    setValues(current => ({...current, [field]: value}));
    setErrors(current => ({...current, [field]: ''}));
  }

  function validate(): boolean {
    const nextErrors: Record<string, string> = {};
    const visitErrors = validateVisitForm(values, 'create');
    for (const field of VISIT_FIELD_ORDER) {
      const error = visitErrors[field];
      if (error) {
        nextErrors[field] = validationT(error.key, error.values);
      }
    }

    const hasDiagnosis = diagnosisName.trim() !== '' || diagnosisDescription.trim() !== '';
    if (hasDiagnosis && !diagnosisName.trim()) {
      nextErrors.diagnosisName = validationT('diagnosisNameRequired');
    }
    if (!diagnosisDate.trim()) {
      if (hasDiagnosis) nextErrors.diagnosisDate = validationT('diagnosisDateRequired');
    }

    const hasVitals = VITAL_SIGNS_FIELDS.some(field => vitals[field].trim() !== '');
    if (hasVitals) {
      const vitalErrors = validateVitalSignsForm(vitals);
      for (const field of VITAL_SIGNS_FIELDS) {
        const error = vitalErrors[field];
        if (error) {
          nextErrors[`vital-${field}`] = vitalT(`validation.${error.key}`, error.values);
        }
      }
    }

    for (const medication of medications) {
      const hasItem = medication.medicationName.trim() || medication.dosage.trim() ||
        medication.frequency.trim() || medication.duration.trim() || medication.instructions.trim();
      if (!hasItem) continue;
      if (!medication.medicationName.trim()) nextErrors[`medication-${medication.id}-name`] = t('medicationNameRequired');
      if (!medication.dosage.trim()) nextErrors[`medication-${medication.id}-dosage`] = t('dosageRequired');
      if (!medication.frequency.trim()) nextErrors[`medication-${medication.id}-frequency`] = t('frequencyRequired');
    }

    setErrors(nextErrors);
    const firstErrorKey = Object.keys(nextErrors)[0];
    if (firstErrorKey) {
      document.getElementById(
        firstErrorKey.startsWith('medication-')
          ? firstErrorKey
          : firstErrorKey.startsWith('vital-')
            ? `unified-vital-${firstErrorKey.replace('vital-', '')}`
            : firstErrorKey === 'diagnosisName'
              ? 'unified-diagnosis-name'
              : firstErrorKey === 'diagnosisDate'
                ? 'unified-diagnosis-date'
                : VISIT_FIELD_IDS[firstErrorKey as keyof typeof VISIT_FIELD_IDS]
      )?.focus();
      return false;
    }
    return true;
  }

  function hasVitals(): boolean {
    return VITAL_SIGNS_FIELDS.some(field => vitals[field].trim() !== '');
  }

  function activeMedicationRows(): MedicationDraft[] {
    return medications.filter(medication =>
      medication.medicationName.trim() || medication.dosage.trim() ||
      medication.frequency.trim() || medication.duration.trim() || medication.instructions.trim()
    );
  }

  async function saveChildren(
    visitId: string,
    targets: PartialFailures
  ): Promise<PartialFailures> {
    const failures: PartialFailures = {diagnosis: false, vitals: false, medications: []};

    if (targets.diagnosis) {
      setSavePhase(t('savingDiagnosis'));
      const payload: CreateDiagnosisRequest = {
        diagnosisName: diagnosisName.trim(),
        description: trimToNull(diagnosisDescription),
        diagnosisDate: diagnosisDate.trim()
      };
      try {
        await createDiagnosis(visitId, payload);
      } catch {
        failures.diagnosis = true;
      }
    }

    if (targets.vitals) {
      setSavePhase(t('savingVitals'));
      const token = readMedicalToken();
      if (!token) {
        failures.vitals = true;
      } else {
        const payload: SaveVitalSignsRequest = toVitalSignsRequest(vitals);
        try {
          await createVitalSigns(visitId, payload, token);
        } catch {
          failures.vitals = true;
        }
      }
    }

    for (const medication of activeMedicationRows()) {
      if (!targets.medications.includes(medication.id)) continue;
      setSavePhase(t('savingPrescription'));
      const payload: CreateMedicationRequest = {
        medicationName: medication.medicationName.trim(),
        dosage: medication.dosage.trim(),
        frequency: medication.frequency.trim(),
        duration: trimToNull(medication.duration),
        instructions: trimToNull(medication.instructions)
      };
      try {
        await createMedication(visitId, payload);
      } catch {
        failures.medications.push(medication.id);
      }
    }
    return failures;
  }

  async function finishVisit(visit: MedicalVisitResponse) {
    await Promise.all([
      queryClient.invalidateQueries({queryKey: medicalRecordKeys.visit(visit.id)}),
      queryClient.invalidateQueries({queryKey: medicalRecordKeys.all})
    ]);
    onSuccess(visit);
  }

  async function submitUnified(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (lock.current) return;
    if (!validate()) return;

    lock.current = true;
    setIsSaving(true);
    setPartialFailures(null);

    try {
      let visit: MedicalVisitResponse;
      let visitId = createdVisitId;

      if (createdVisitId && createdVisit) {
        visit = createdVisit;
        visitId = createdVisitId;
      } else {
        setSavePhase(t('savingVisit'));
        const payload: CreateMedicalVisitRequest = {
          patientId: values.patientId.trim(),
          appointmentId: trimToNull(values.appointmentId),
          visitDate: toVisitDateIso(values.visitDate),
          chiefComplaint: trimToNull(values.chiefComplaint),
          symptoms: trimToNull(values.symptoms),
          clinicalNotes: trimToNull(values.clinicalNotes)
        };
        visit = await createMedicalVisit(payload);
        visitId = visit.id;
        setCreatedVisit(visit);
        setCreatedVisitId(visitId);
      }

      const targets: PartialFailures = {
        diagnosis: diagnosisName.trim() !== '' || diagnosisDescription.trim() !== '',
        vitals: hasVitals(),
        medications: activeMedicationRows().map(medication => medication.id)
      };

      const failures = await saveChildren(visitId, targets);
      if (!failures.diagnosis && !failures.vitals && failures.medications.length === 0) {
        setSavePhase(t('complete'));
        await finishVisit(visit);
        return;
      }

      setPartialFailures(failures);
    } catch {
      setPartialFailures({diagnosis: false, vitals: false, medications: []});
      setSavePhase(t('visitSaveFailed'));
    } finally {
      setIsSaving(false);
      lock.current = false;
    }
  }

  async function retryFailures() {
    if (!createdVisitId || !createdVisit || !partialFailures || lock.current) return;
    lock.current = true;
    setIsSaving(true);
    try {
      const failures = await saveChildren(createdVisitId, partialFailures);
      if (!failures.diagnosis && !failures.vitals && failures.medications.length === 0) {
        await finishVisit(createdVisit);
        return;
      }
      setPartialFailures(failures);
    } finally {
      setIsSaving(false);
      lock.current = false;
    }
  }

  function addMedication() {
    setMedications(current => [
      ...current,
      {
        id: nextMedicationId.current++,
        medicationName: '',
        dosage: '',
        frequency: '',
        duration: '',
        instructions: ''
      }
    ]);
  }

  function updateMedication(id: number, field: keyof Omit<MedicationDraft, 'id'>, value: string) {
    setMedications(current => current.map(item => item.id === id ? {...item, [field]: value} : item));
  }

  return (
    <form onSubmit={submitUnified} noValidate className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      {partialFailures ? (
        <div role="alert" className="rounded-xl border border-danger/40 bg-danger/10 p-4">
          <p className="text-sm font-bold text-danger">{t('partialFailure')}</p>
          <p className="mt-1 text-xs leading-6 text-muted">{t('partialFailureDescription')}</p>
          <button type="button" onClick={retryFailures} disabled={isSaving} className={`${primaryButtonClassName} mt-3`}>
            {isSaving ? t('retrying') : t('retryPartial')}
          </button>
        </div>
      ) : null}

      {isSaving ? (
        <p role="status" className="rounded-xl border border-brand/30 bg-brand-soft px-4 py-3 text-sm font-semibold text-brand-strong">
          {savePhase}
        </p>
      ) : null}

      <section className="rounded-2xl border border-border bg-background p-5 sm:p-6">
        <header className="mb-5">
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-brand">{t('visitSection')}</p>
          <h2 className="mt-2 text-xl font-black">{visitT('createTitle')}</h2>
        </header>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id={VISIT_FIELD_IDS.patientId} label={visitT('patientId')} required error={errors.patientId}>
            {control => (
              <PatientSelect
                id={control.id}
                value={patient}
                disabled={isSaving}
                error={errors.patientId}
                onChange={next => {
                  setPatient(next);
                  setVisitField('patientId', next?.id ?? '');
                }}
              />
            )}
          </FormField>
          <FormField id={VISIT_FIELD_IDS.visitDate} label={visitT('visitDate')} required error={errors.visitDate}>
            {control => <input {...control} type="datetime-local" value={values.visitDate}
              onChange={event => setVisitField('visitDate', event.target.value)} className={controlClassName} />}
          </FormField>
          <FormField id={VISIT_FIELD_IDS.appointmentId} label={visitT('appointmentId')} error={errors.appointmentId}>
            {control => <input {...control} type="text" dir="ltr" value={values.appointmentId}
              onChange={event => setVisitField('appointmentId', event.target.value)} className={`${controlClassName} font-mono`} />}
          </FormField>
          <div />
          <FormField id={VISIT_FIELD_IDS.chiefComplaint} label={visitT('chiefComplaint')} className="flex flex-col gap-1.5 sm:col-span-2" error={errors.chiefComplaint}>
            {control => <textarea {...control} rows={3} value={values.chiefComplaint}
              onChange={event => setVisitField('chiefComplaint', event.target.value)} className={textareaClassName} />}
          </FormField>
          <FormField id={VISIT_FIELD_IDS.symptoms} label={visitT('symptoms')} className="flex flex-col gap-1.5 sm:col-span-2" error={errors.symptoms}>
            {control => <textarea {...control} rows={3} value={values.symptoms}
              onChange={event => setVisitField('symptoms', event.target.value)} className={textareaClassName} />}
          </FormField>
          <FormField id={VISIT_FIELD_IDS.clinicalNotes} label={visitT('clinicalNotes')} className="flex flex-col gap-1.5 sm:col-span-2" error={errors.clinicalNotes}>
            {control => <textarea {...control} rows={4} value={values.clinicalNotes}
              onChange={event => setVisitField('clinicalNotes', event.target.value)} className={textareaClassName} />}
          </FormField>
        </div>
      </section>

      <section className="rounded-2xl border border-border bg-background p-5 sm:p-6">
        <header className="mb-5">
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-brand">{t('diagnosisSection')}</p>
          <h2 className="mt-2 text-xl font-black">{t('diagnosisTitle')}</h2>
        </header>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField id="unified-diagnosis-name" label={t('diagnosisName')} error={errors.diagnosisName}>
            {control => <input {...control} type="text" value={diagnosisName}
              onChange={event => setDiagnosisName(event.target.value)} className={controlClassName} />}
          </FormField>
          <FormField id="unified-diagnosis-date" label={t('diagnosisDate')} error={errors.diagnosisDate}>
            {control => <input {...control} type="date" min="1900-01-01" max={latestDiagnosisDateOnly()}
              value={diagnosisDate} onChange={event => setDiagnosisDate(event.target.value)} className={controlClassName} />}
          </FormField>
          <FormField id="unified-diagnosis-description" label={t('diagnosisDescription')} className="flex flex-col gap-1.5 sm:col-span-2">
            {control => <textarea {...control} rows={3} value={diagnosisDescription}
              onChange={event => setDiagnosisDescription(event.target.value)} className={textareaClassName} />}
          </FormField>
        </div>
      </section>

      <section className="rounded-2xl border border-border bg-background p-5 sm:p-6">
        <header className="mb-5">
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-brand">{t('vitalsSection')}</p>
          <h2 className="mt-2 text-xl font-black">{t('vitalsTitle')}</h2>
        </header>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {VITAL_SIGNS_FIELDS.map(field => (
            <FormField
              key={field}
              id={`unified-vital-${field}`}
              label={vitalT(`fields.${field}`)}
              hint={vitalT(`units.${field}`)}
              error={errors[`vital-${field}`]}
            >
              {control => <input {...control} type="text" dir="ltr" inputMode={field === 'bloodPressure' ? 'text' : 'decimal'}
                value={vitals[field]} onChange={event => setVitals(current => ({...current, [field]: event.target.value}))}
                className={controlClassName} />}
            </FormField>
          ))}
        </div>
      </section>

      <section className="rounded-2xl border border-border bg-background p-5 sm:p-6">
        <header className="mb-5 flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.18em] text-brand">{t('prescriptionSection')}</p>
            <h2 className="mt-2 text-xl font-black">{t('prescriptionTitle')}</h2>
          </div>
          <button type="button" onClick={addMedication} className={secondaryButtonClassName}>
            <FiPlus aria-hidden className="size-4" />
            {t('addMedication')}
          </button>
        </header>
        <div className="flex flex-col gap-4">
          {medications.map((medication, index) => (
            <fieldset key={medication.id} className="grid gap-4 rounded-xl border border-border p-4 sm:grid-cols-2 lg:grid-cols-3">
              <legend className="px-2 text-xs font-bold text-muted">{t('medicationNumber', {number: index + 1})}</legend>
              <FormField id={`medication-${medication.id}-name`} label={t('medicationName')} required error={errors[`medication-${medication.id}-name`]}>
                {control => <input {...control} type="text" value={medication.medicationName}
                  onChange={event => updateMedication(medication.id, 'medicationName', event.target.value)} className={controlClassName} />}
              </FormField>
              <FormField id={`medication-${medication.id}-dosage`} label={t('dosage')} required error={errors[`medication-${medication.id}-dosage`]}>
                {control => <input {...control} type="text" value={medication.dosage}
                  onChange={event => updateMedication(medication.id, 'dosage', event.target.value)} className={controlClassName} />}
              </FormField>
              <FormField id={`medication-${medication.id}-frequency`} label={t('frequency')} required error={errors[`medication-${medication.id}-frequency`]}>
                {control => <input {...control} type="text" value={medication.frequency}
                  onChange={event => updateMedication(medication.id, 'frequency', event.target.value)} className={controlClassName} />}
              </FormField>
              <FormField id={`medication-${medication.id}-duration`} label={t('duration')}>
                {control => <input {...control} type="text" value={medication.duration}
                  onChange={event => updateMedication(medication.id, 'duration', event.target.value)} className={controlClassName} />}
              </FormField>
              <FormField id={`medication-${medication.id}-instructions`} label={t('instructions')} className="flex flex-col gap-1.5 sm:col-span-2 lg:col-span-2">
                {control => <textarea {...control} rows={3} value={medication.instructions}
                  onChange={event => updateMedication(medication.id, 'instructions', event.target.value)} className={textareaClassName} />}
              </FormField>
              {medications.length > 1 ? (
                <button type="button" onClick={() => setMedications(current => current.filter(item => item.id !== medication.id))}
                  className="inline-flex h-10 w-fit items-center gap-2 self-end rounded-xl border border-danger/40 px-3 text-sm font-bold text-danger">
                  <FiTrash2 aria-hidden className="size-4" />
                  {t('removeMedication')}
                </button>
              ) : null}
            </fieldset>
          ))}
        </div>
      </section>

      <footer className="sticky bottom-3 z-10 mx-auto flex w-full max-w-5xl flex-wrap items-center justify-end gap-3 rounded-2xl border border-border bg-surface/95 p-4 shadow-xl backdrop-blur">
        {onCancel ? (
          <button type="button" onClick={onCancel} disabled={isSaving} className={secondaryButtonClassName}>
            <FiX aria-hidden className="size-4" />
            {commonT('cancel')}
          </button>
        ) : null}
        <button type="submit" disabled={isSaving} className={primaryButtonClassName}>
          <FiSave aria-hidden className="size-4" />
          {isSaving ? t('saving') : t('saveVisit')}
        </button>
      </footer>
    </form>
  );
}
