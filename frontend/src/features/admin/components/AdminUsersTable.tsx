'use client';

import {useTranslations} from 'next-intl';
import type {AdminRole, AdminStatus, AdminUser} from '../types';

type AdminUsersTableProps = {
  users: readonly AdminUser[];
  isLoading: boolean;
  currentUserId: string | null;
  onStatus: (user: AdminUser) => void;
  onRole: (user: AdminUser) => void;
  onAssign: (user: AdminUser) => void;
};

const CELL_CLASS = 'px-4 py-3 align-middle text-sm';
const BUTTON_CLASS =
  'rounded-lg border border-border px-2.5 py-1.5 text-xs font-semibold transition-colors hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50';

export function AdminUsersTable({
  users,
  isLoading,
  currentUserId,
  onStatus,
  onRole,
  onAssign
}: AdminUsersTableProps) {
  const t = useTranslations('Admin');

  function roleLabel(role: AdminRole): string {
    return t(role === 'Admin' ? 'roles.Admin' : role === 'Doctor' ? 'roles.Doctor' : 'roles.Secretary');
  }

  function statusLabel(status: AdminStatus): string {
    return t(status === 'Active' ? 'statuses.Active' : 'statuses.Inactive');
  }

  return (
    <div className="overflow-x-auto rounded-2xl border border-border bg-surface">
      <table className="w-full min-w-[900px] border-collapse text-start">
        <caption className="sr-only">{t('table.caption')}</caption>
        <thead className="bg-surface-2 text-start text-xs font-bold uppercase tracking-wide text-muted">
          <tr>
            <th scope="col" className={`${CELL_CLASS} text-start`}>
              {t('table.name')}
            </th>
            <th scope="col" className={`${CELL_CLASS} text-start`}>
              {t('table.email')}
            </th>
            <th scope="col" className={`${CELL_CLASS} text-start`}>
              {t('table.role')}
            </th>
            <th scope="col" className={`${CELL_CLASS} text-start`}>
              {t('table.status')}
            </th>
            <th scope="col" className={`${CELL_CLASS} text-start`}>
              {t('table.verified')}
            </th>
            <th scope="col" className={`${CELL_CLASS} text-start`}>
              {t('table.created')}
            </th>
            <th scope="col" className={`${CELL_CLASS} text-start`}>
              {t('table.actions')}
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {isLoading && users.length === 0 ? (
            <tr>
              <td colSpan={7} className={`${CELL_CLASS} text-center text-muted`}>
                {t('states.loading')}
              </td>
            </tr>
          ) : (
            users.map((user) => (
              <tr key={user.id}>
                <td className={CELL_CLASS}>
                  <span className="font-semibold">{user.fullName}</span>
                  {user.id === currentUserId ? (
                    <span className="ms-2 text-xs text-muted">({t('table.you')})</span>
                  ) : null}
                </td>
                <td className={`${CELL_CLASS} text-muted`} dir="ltr">
                  {user.email}
                </td>
                <td className={CELL_CLASS}>{roleLabel(user.role)}</td>
                <td className={CELL_CLASS}>
                  <span
                    className={
                      user.status === 'Active'
                        ? 'inline-flex whitespace-nowrap rounded-full bg-emerald-500/10 px-2.5 py-1 text-xs font-bold text-emerald-600'
                        : 'inline-flex whitespace-nowrap rounded-full bg-amber-500/10 px-2.5 py-1 text-xs font-bold text-amber-600'
                    }
                  >
                    {statusLabel(user.status)}
                  </span>
                </td>
                <td className={CELL_CLASS}>
                  {t(user.emailVerified ? 'flags.yes' : 'flags.no')}
                </td>
                <td className={`${CELL_CLASS} text-muted`} dir="ltr">
                  {new Date(user.createdAtUtc).toLocaleDateString()}
                </td>
                <td className={CELL_CLASS}>
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      className={BUTTON_CLASS}
                      disabled={user.id === currentUserId}
                      onClick={() => onStatus(user)}
                    >
                      {t(user.status === 'Active' ? 'actions.deactivate' : 'actions.activate')}
                    </button>
                    {user.role !== 'Admin' ? (
                      <button
                        type="button"
                        className={BUTTON_CLASS}
                        disabled={user.id === currentUserId}
                        onClick={() => onRole(user)}
                      >
                        {t('actions.changeRole')}
                      </button>
                    ) : null}
                    {user.role === 'Secretary' ? (
                      <button type="button" className={BUTTON_CLASS} onClick={() => onAssign(user)}>
                        {t('actions.assignDoctors')}
                      </button>
                    ) : null}
                  </div>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
