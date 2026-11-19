'use client';

import {useMemo, useState} from 'react';
import {useTranslations} from 'next-intl';
import {FiSearch} from 'react-icons/fi';
import {
  useAdminDoctorOptions,
  useChangeAdminUserRole,
  useReplaceSecretaryDoctors,
  useSecretaryDoctors,
  useSetAdminUserStatus
} from '../hooks';
import {
  ADMIN_SEARCH_MAX_LENGTH,
  MAX_DOCTOR_ASSIGNMENTS,
  type AdminDoctor,
  type AdminRole,
  type AdminStatus,
  type AdminUser
} from '../types';
import {AdminDialog} from './AdminDialog';

const PRIMARY_BUTTON =
  'inline-flex h-10 items-center justify-center rounded-xl bg-brand px-4 text-sm font-bold text-[#06211f] transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50';
const SECONDARY_BUTTON =
  'inline-flex h-10 items-center justify-center rounded-xl border border-border px-4 text-sm font-semibold transition-colors hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50';
const INPUT_CLASS =
  'w-full rounded-xl border border-border bg-background px-3 py-2 text-sm outline-none transition-colors focus:border-brand focus:ring-2 focus:ring-brand/30';

type DialogProps = {
  user: AdminUser | null;
  onClose: () => void;
};

export function StatusDialog({user, onClose}: DialogProps) {
  const t = useTranslations('Admin');
  const mutation = useSetAdminUserStatus();
  const nextStatus: AdminStatus = user?.status === 'Active' ? 'Inactive' : 'Active';
  const statusLabel = t(nextStatus === 'Active' ? 'statuses.Active' : 'statuses.Inactive');
  const actionLabel = t(nextStatus === 'Active' ? 'actions.activate' : 'actions.deactivate');

  return (
    <AdminDialog
      open={user !== null}
      onClose={onClose}
      title={t('dialogs.statusTitle')}
      description={user ? t('dialogs.statusMessage', {name: user.fullName, status: statusLabel}) : ''}
    >
      <div className="flex flex-wrap justify-end gap-2">
        <button type="button" className={SECONDARY_BUTTON} onClick={onClose}>
          {t('dialogs.cancel')}
        </button>
        <button
          type="button"
          className={PRIMARY_BUTTON}
          disabled={mutation.isPending || user === null}
          onClick={() => {
            if (!user) {
              return;
            }

            mutation.mutate(
              {id: user.id, status: nextStatus},
              {onSuccess: () => onClose()}
            );
          }}
        >
          {actionLabel}
        </button>
      </div>
    </AdminDialog>
  );
}

export function RoleDialog({user, onClose}: DialogProps) {
  const t = useTranslations('Admin');
  const mutation = useChangeAdminUserRole();
  const defaultRole: Extract<AdminRole, 'Doctor' | 'Secretary'> =
    user?.role === 'Doctor' ? 'Secretary' : 'Doctor';
  const [role, setRole] = useState<Extract<AdminRole, 'Doctor' | 'Secretary'>>(defaultRole);

  return (
    <AdminDialog
      open={user !== null}
      onClose={onClose}
      title={t('dialogs.roleTitle')}
      description={user ? t('dialogs.roleMessage', {name: user.fullName}) : ''}
    >
      <div className="flex flex-col gap-4">
        <label className="flex flex-col gap-2 text-sm font-medium">
          {t('dialogs.newRoleLabel')}
          <select
            value={role}
            onChange={(event) =>
              setRole(event.target.value as Extract<AdminRole, 'Doctor' | 'Secretary'>)
            }
            className={INPUT_CLASS}
          >
            <option value="Doctor">{t('roles.Doctor')}</option>
            <option value="Secretary">{t('roles.Secretary')}</option>
          </select>
        </label>

        <div className="flex flex-wrap justify-end gap-2">
          <button type="button" className={SECONDARY_BUTTON} onClick={onClose}>
            {t('dialogs.cancel')}
          </button>
          <button
            type="button"
            className={PRIMARY_BUTTON}
            disabled={mutation.isPending || user === null || role === user?.role}
            onClick={() => {
              if (!user) {
                return;
              }

              mutation.mutate({id: user.id, role}, {onSuccess: () => onClose()});
            }}
          >
            {t('dialogs.confirm')}
          </button>
        </div>
      </div>
    </AdminDialog>
  );
}

export function AssignmentDialog({user, onClose}: DialogProps) {
  const t = useTranslations('Admin');
  const mutation = useReplaceSecretaryDoctors();
  const isSecretary = user?.role === 'Secretary';
  const secretaryId = isSecretary ? (user?.id ?? null) : null;
  const assignments = useSecretaryDoctors(secretaryId, user !== null && isSecretary);
  const [search, setSearch] = useState('');
  // `null` means "use the server-provided assignment set until the admin edits it". This avoids
  // copying query data into local state while still allowing an unsaved draft selection.
  const [draftIds, setDraftIds] = useState<readonly string[] | null>(null);
  const serverIds = assignments.data?.doctors.map((doctor) => doctor.id) ?? [];
  const selectedIds = draftIds ?? serverIds;

  const doctorOptions = useAdminDoctorOptions(search, user !== null && isSecretary);

  const selectedDoctors = useMemo(() => {
    const candidates = new Map<string, AdminDoctor>();
    const visible = doctorOptions.data?.items ?? [];

    for (const doctor of [...(assignments.data?.doctors ?? []), ...visible]) {
      candidates.set(doctor.id, doctor);
    }

    return selectedIds
      .map((doctorId) => candidates.get(doctorId))
      .filter((doctor): doctor is AdminDoctor => doctor !== undefined);
  }, [assignments.data, doctorOptions.data, selectedIds]);

  const visibleDoctors = doctorOptions.data?.items ?? [];
  const isAtLimit = selectedIds.length >= MAX_DOCTOR_ASSIGNMENTS;

  function toggleDoctor(doctorId: string): void {
    setDraftIds((current) => {
      const base = current ?? serverIds;

      return base.includes(doctorId)
        ? base.filter((id) => id !== doctorId)
        : isAtLimit
          ? base
          : [...base, doctorId];
    });
  }

  return (
    <AdminDialog
      open={user !== null && isSecretary}
      onClose={onClose}
      title={t('dialogs.assignmentTitle')}
      description={user ? t('dialogs.assignmentMessage', {name: user.fullName}) : ''}
    >
      <div className="flex flex-col gap-4">
        <label className="flex flex-col gap-2 text-sm font-medium">
          <span className="inline-flex items-center gap-2">
            <FiSearch aria-hidden className="size-4 text-brand" />
            {t('dialogs.doctorSearchLabel')}
          </span>
          <input
            type="search"
            value={search}
            maxLength={ADMIN_SEARCH_MAX_LENGTH}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t('dialogs.doctorSearchPlaceholder')}
            className={INPUT_CLASS}
          />
        </label>

        <div>
          <p className="text-xs font-bold uppercase tracking-wide text-muted">
            {t('dialogs.selectedLabel')}
          </p>
          <div className="mt-2 flex flex-wrap gap-2">
            {selectedDoctors.length === 0 ? (
              <span className="text-sm text-muted">—</span>
            ) : (
              selectedDoctors.map((doctor) => (
                <button
                  key={doctor.id}
                  type="button"
                  onClick={() => toggleDoctor(doctor.id)}
                  className="rounded-full bg-brand-soft px-3 py-1 text-xs font-semibold text-brand"
                  aria-label={doctor.fullName}
                >
                  {doctor.fullName}
                </button>
              ))
            )}
          </div>
        </div>

        <div className="max-h-52 overflow-y-auto rounded-xl border border-border">
          {doctorOptions.isPending ? (
            <p className="p-4 text-sm text-muted">{t('states.loading')}</p>
          ) : visibleDoctors.length === 0 ? (
            <p className="p-4 text-sm text-muted">{t('dialogs.noDoctors')}</p>
          ) : (
            <ul className="divide-y divide-border">
              {visibleDoctors.map((doctor) => {
                const checked = selectedIds.includes(doctor.id);

                return (
                  <li key={doctor.id}>
                    <label className="flex cursor-pointer items-center gap-3 p-3 text-sm">
                      <input
                        type="checkbox"
                        checked={checked}
                        disabled={!checked && isAtLimit}
                        onChange={() => toggleDoctor(doctor.id)}
                        className="size-4 accent-[var(--color-brand)]"
                      />
                      <span className="min-w-0">
                        <span className="block font-semibold">{doctor.fullName}</span>
                        <span className="block truncate text-xs text-muted" dir="ltr">
                          {doctor.email}
                        </span>
                      </span>
                    </label>
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        <div className="flex flex-wrap justify-end gap-2">
          <button type="button" className={SECONDARY_BUTTON} onClick={onClose}>
            {t('dialogs.cancel')}
          </button>
          <button
            type="button"
            className={PRIMARY_BUTTON}
            disabled={mutation.isPending || secretaryId === null || assignments.isPending}
            onClick={() => {
              if (!secretaryId) {
                return;
              }

              mutation.mutate(
                {secretaryId, doctorIds: selectedIds},
                {onSuccess: () => onClose()}
              );
            }}
          >
            {t('dialogs.confirm')}
          </button>
        </div>
      </div>
    </AdminDialog>
  );
}
