'use client';

import {useState} from 'react';
import {useTranslations} from 'next-intl';
import {FiActivity, FiCheckCircle, FiRefreshCw, FiShield, FiUsers} from 'react-icons/fi';
import {useSession} from '@/features/auth';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';
import {useAdminErrorMessage, useAdminUsers} from '../hooks';
import type {AdminUser, AdminUserFilters} from '../types';
import {AdminFilters} from './AdminFilters';
import {AdminUsersTable} from './AdminUsersTable';
import {AssignmentDialog, RoleDialog, StatusDialog} from './dialogs';

const INITIAL_FILTERS: AdminUserFilters = {
  search: '',
  role: 'all',
  status: 'all',
  pageNumber: 1,
  pageSize: DEFAULT_PAGE_SIZE
};

const PAGINATION_BUTTON =
  'inline-flex h-10 items-center justify-center rounded-xl border border-border px-4 text-sm font-semibold transition-colors hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50';

export function AdminDashboardScreen() {
  const t = useTranslations('Admin');
  const session = useSession();
  const getErrorMessage = useAdminErrorMessage();
  const [filters, setFilters] = useState<AdminUserFilters>(INITIAL_FILTERS);
  const [statusUser, setStatusUser] = useState<AdminUser | null>(null);
  const [roleUser, setRoleUser] = useState<AdminUser | null>(null);
  const [assignmentUser, setAssignmentUser] = useState<AdminUser | null>(null);

  const query = useAdminUsers(filters);
  const users = query.data?.items ?? [];
  const isEmpty = !query.isPending && !query.isError && users.length === 0;
  const activeCount = users.filter((user) => user.status === 'Active').length;
  const verifiedCount = users.filter((user) => user.emailVerified).length;
  const doctorsCount = users.filter((user) => user.role === 'Doctor').length;
  const secretariesCount = users.filter((user) => user.role === 'Secretary').length;
  const roleTotal = Math.max(doctorsCount + secretariesCount, 1);

  function updateFilters(patch: Partial<AdminUserFilters>): void {
    setFilters((current) => ({...current, ...patch}));
  }

  function clearFilters(): void {
    setFilters(INITIAL_FILTERS);
  }

  return (
    <main className="mx-auto flex w-[min(1180px,calc(100%-40px))] flex-col gap-5 py-8">
      <header>
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand">
          {t('page.eyebrow')}
        </p>
        <h1 className="mt-2 text-3xl font-black sm:text-4xl">{t('page.title')}</h1>
        <p className="mt-2 max-w-3xl text-sm leading-relaxed text-muted">
          {t('page.description')}
        </p>
      </header>

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4" aria-label={t('overview.label')}>
        {[
          {label: t('overview.total'), value: query.data?.totalCount ?? 0, icon: FiUsers, tone: 'text-brand bg-brand-soft'},
          {label: t('overview.active'), value: activeCount, icon: FiActivity, tone: 'text-emerald-500 bg-emerald-500/10'},
          {label: t('overview.verified'), value: verifiedCount, icon: FiCheckCircle, tone: 'text-sky-500 bg-sky-500/10'},
          {label: t('overview.page'), value: users.length, icon: FiShield, tone: 'text-amber-500 bg-amber-500/10'}
        ].map((stat) => {
          const Icon = stat.icon;
          return (
            <article key={stat.label} className="rounded-2xl border border-border bg-surface p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-brand/40">
              <div className="flex items-start justify-between gap-3">
                <p className="text-xs font-semibold text-muted">{stat.label}</p>
                <span className={`grid size-10 place-items-center rounded-xl ${stat.tone}`}><Icon className="size-5" /></span>
              </div>
              <p className="mt-4 text-3xl font-black tracking-tight">{query.isPending ? '—' : stat.value.toLocaleString()}</p>
            </article>
          );
        })}
      </section>

      <section className="grid gap-5 lg:grid-cols-[1.2fr_.8fr]">
        <div className="rounded-2xl border border-border bg-surface p-5 shadow-sm">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="text-base font-black">{t('overview.distributionTitle')}</h2>
              <p className="mt-1 text-xs text-muted">{t('overview.distributionHint')}</p>
            </div>
            <FiUsers className="size-5 text-brand" aria-hidden />
          </div>
          <div className="mt-6 grid gap-4">
            {[
              {label: t('roles.Doctor'), value: doctorsCount, tone: 'bg-brand'},
              {label: t('roles.Secretary'), value: secretariesCount, tone: 'bg-sky-500'}
            ].map((item) => (
              <div key={item.label}>
                <div className="mb-2 flex justify-between text-xs font-bold">
                  <span>{item.label}</span><span className="text-muted">{item.value}</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-surface-2">
                  <div className={`h-full rounded-full ${item.tone} transition-all`} style={{width: `${(item.value / roleTotal) * 100}%`}} />
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="rounded-2xl border border-brand/20 bg-gradient-to-br from-brand/15 via-surface to-surface p-5 shadow-sm">
          <p className="text-xs font-bold uppercase tracking-[0.18em] text-brand">{t('overview.liveEyebrow')}</p>
          <h2 className="mt-3 text-xl font-black">{t('overview.liveTitle')}</h2>
          <p className="mt-2 text-sm leading-7 text-muted">{t('overview.liveDescription')}</p>
          <div className="mt-5 flex items-center gap-2 text-xs font-bold text-brand">
            <span className="size-2 rounded-full bg-brand shadow-[0_0_0_5px_rgba(45,212,191,.12)]" />
            {t('overview.liveStatus')}
          </div>
        </div>
      </section>

      <AdminFilters
        filters={filters}
        onChange={updateFilters}
        onClear={clearFilters}
      />

      {query.isError && !query.data ? (
        <div role="alert" className="rounded-2xl border border-border bg-surface p-6 text-center">
          <h2 className="text-lg font-bold">{t('states.errorTitle')}</h2>
          <p className="mt-1 text-sm text-muted">{getErrorMessage(query.error)}</p>
          <button
            type="button"
            onClick={() => query.refetch()}
            className="mt-4 inline-flex h-10 items-center justify-center gap-2 rounded-xl border border-border px-4 text-sm font-semibold transition-colors hover:bg-surface-2"
          >
            <FiRefreshCw aria-hidden className="size-4" />
            {t('states.retry')}
          </button>
        </div>
      ) : isEmpty ? (
        <div className="rounded-2xl border border-border bg-surface p-6 text-center">
          <p className="text-sm font-semibold">{t('states.empty')}</p>
          {filters.search !== '' || filters.role !== 'all' || filters.status !== 'all' ? (
            <button
              type="button"
              onClick={clearFilters}
              className="mt-4 inline-flex h-10 items-center justify-center rounded-xl border border-border px-4 text-sm font-semibold transition-colors hover:bg-surface-2"
            >
              {t('filters.clear')}
            </button>
          ) : null}
        </div>
      ) : (
        <>
          <AdminUsersTable
            users={users}
            isLoading={query.isPending}
            currentUserId={session.user?.id ?? null}
            onStatus={setStatusUser}
            onRole={setRoleUser}
            onAssign={setAssignmentUser}
          />

          {query.data ? (
            <div className="flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-muted" aria-current="page">
                {t('states.count', {count: query.data.totalCount})}
              </p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  className={PAGINATION_BUTTON}
                  disabled={filters.pageNumber <= 1}
                  onClick={() => updateFilters({pageNumber: filters.pageNumber - 1})}
                >
                  {t('states.previousPage')}
                </button>
                <span className="text-xs font-semibold text-muted">
                  {t('states.pageStatus', {
                    page: query.data.page,
                    totalPages: Math.max(query.data.totalPages, 1)
                  })}
                </span>
                <button
                  type="button"
                  className={PAGINATION_BUTTON}
                  disabled={filters.pageNumber >= query.data.totalPages}
                  onClick={() => updateFilters({pageNumber: filters.pageNumber + 1})}
                >
                  {t('states.nextPage')}
                </button>
              </div>
            </div>
          ) : null}
        </>
      )}

      <StatusDialog user={statusUser} onClose={() => setStatusUser(null)} />
      <RoleDialog
        key={roleUser?.id ?? 'role-none'}
        user={roleUser}
        onClose={() => setRoleUser(null)}
      />
      <AssignmentDialog
        key={assignmentUser?.id ?? 'assignment-none'}
        user={assignmentUser}
        onClose={() => setAssignmentUser(null)}
      />
    </main>
  );
}

export function AdminPageFallback() {
  return (
    <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8">
      <div className="h-9 w-64 animate-pulse rounded bg-surface-2" />
      <div className="mt-6 h-56 animate-pulse rounded-2xl bg-surface-2" />
    </main>
  );
}
