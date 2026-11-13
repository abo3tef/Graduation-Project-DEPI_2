'use client';

import {useEffect, useState} from 'react';
import {FiEdit3, FiPlus, FiTrash2} from 'react-icons/fi';
import {useTranslations} from 'next-intl';
import {useDeleteMedicationMutation} from '../hooks/use-medication-mutations';
import type {MedicationResponse} from '../types';
import {readDevToken} from '../use-dev-token';
import {ConfirmDeleteDialog} from './ConfirmDeleteDialog';
import {MedicationForm} from './MedicationForm';
import {deleteActionClassName, sectionActionClassName} from './form-styles';

type MedicationSectionProps = {
  visitId: string;
  medications: MedicationResponse[];
};

/**
 * Medication list for a visit. Delete is a permanent `DELETE` (D-23), so it always goes through
 * `ConfirmDeleteDialog`; the row only disappears after the mutation succeeds and the parent visit
 * query is invalidated, so the UI never shows an optimistic state that the server rejected.
 */
export function MedicationSection({visitId, medications}: MedicationSectionProps) {
  const t = useTranslations('MedicalRecords.medication');
  const commonT = useTranslations('MedicalRecords.common');
  const deleteMutation = useDeleteMedicationMutation(visitId);
  const [isCreating, setIsCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<MedicationResponse | null>(
    null
  );

  /**
   * A successful delete unmounts the row the dialog was opened from, so the dialog's own focus
   * restore has nothing left to focus. Land on the section's add button instead of `<body>`.
   */
  useEffect(() => {
    if (!deleteMutation.isSuccess) {
      return;
    }

    document.getElementById('medications-add')?.focus();
  }, [deleteMutation.isSuccess]);

  function closeForms() {
    setIsCreating(false);
    setEditingId(null);
  }

  function startCreate() {
    setEditingId(null);
    setIsCreating(true);
  }

  function startEdit(medicationId: string) {
    setIsCreating(false);
    setEditingId(medicationId);
  }

  function cancelDelete() {
    if (deleteMutation.isPending) {
      return;
    }

    deleteMutation.reset();
    setDeleteTarget(null);
  }

  function confirmDelete() {
    if (!deleteTarget) {
      return;
    }

    const token = readDevToken();

    if (!token) {
      return;
    }

    deleteMutation.mutate(
      {medicationId: deleteTarget.id, token},
      {onSuccess: () => setDeleteTarget(null)}
    );
  }

  return (
    <section
      aria-labelledby="medications-title"
      className="rounded-2xl border border-border bg-surface p-5 sm:p-6"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 id="medications-title" className="text-lg font-bold sm:text-xl">
            {t('title')}
          </h2>
          <p className="mt-1 text-sm text-muted">{t('description')}</p>
        </div>

        {!isCreating ? (
          <button
            id="medications-add"
            type="button"
            onClick={startCreate}
            className={sectionActionClassName}
          >
            <FiPlus aria-hidden className="size-4" />
            {t('addAction')}
          </button>
        ) : null}
      </div>

      <div className="mt-5 flex flex-col gap-3">
        {isCreating ? (
          <MedicationForm
            mode="create"
            visitId={visitId}
            onDone={closeForms}
            onCancel={closeForms}
          />
        ) : null}

        {medications.length === 0 && !isCreating ? (
          <div className="rounded-xl border border-dashed border-border bg-background p-6 text-center">
            <p className="text-sm font-semibold text-foreground">{t('emptyTitle')}</p>
            <p className="mt-1 text-xs text-muted">{t('emptyDescription')}</p>
          </div>
        ) : null}

        {medications.length > 0 ? (
          <ul className="flex flex-col gap-3">
            {medications.map((medication) => (
              <li
                key={medication.id}
                className="rounded-xl border border-border bg-background p-4"
              >
                {editingId === medication.id ? (
                  <MedicationForm
                    mode="edit"
                    visitId={visitId}
                    medication={medication}
                    onDone={closeForms}
                    onCancel={closeForms}
                  />
                ) : (
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <h3 className="text-sm font-bold text-foreground">
                        {medication.medicationName}
                      </h3>
                      <p className="mt-1 text-xs text-muted">
                        {medication.dosage} · {medication.frequency}
                        {medication.duration ? ` · ${medication.duration}` : null}
                      </p>
                      {medication.instructions ? (
                        <p className="mt-2 whitespace-pre-line text-sm text-foreground">
                          {medication.instructions}
                        </p>
                      ) : null}
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      <button
                        type="button"
                        onClick={() => startEdit(medication.id)}
                        aria-label={`${t('editAction')}: ${medication.medicationName}`}
                        className={sectionActionClassName}
                      >
                        <FiEdit3 aria-hidden className="size-4" />
                        {t('editAction')}
                      </button>

                      <button
                        type="button"
                        onClick={() => setDeleteTarget(medication)}
                        aria-label={`${t('deleteAction')}: ${medication.medicationName}`}
                        className={deleteActionClassName}
                      >
                        <FiTrash2 aria-hidden className="size-4" />
                        {t('deleteAction')}
                      </button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        ) : null}
      </div>

      <ConfirmDeleteDialog
        open={deleteTarget !== null}
        title={t('deleteTitle')}
        description={t('deleteDescription')}
        confirmLabel={t('deleteConfirm')}
        pendingLabel={t('deleting')}
        cancelLabel={commonT('cancel')}
        isPending={deleteMutation.isPending}
        error={deleteMutation.error}
        fallbackFocusId="medications-add"
        onConfirm={confirmDelete}
        onCancel={cancelDelete}
      />
    </section>
  );
}
