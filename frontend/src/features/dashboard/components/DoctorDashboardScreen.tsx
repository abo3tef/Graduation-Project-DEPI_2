'use client';

import {useTranslations} from 'next-intl';
import {FiAlertCircle, FiRefreshCw, FiUserCheck} from 'react-icons/fi';
import {useDoctorDashboard} from '../hooks/useDoctorDashboard';
import {DashboardStatCards} from './DashboardStatCards';
import {TodayAppointmentsTable} from './TodayAppointmentsTable';
import {UpcomingAppointmentsPanel} from './UpcomingAppointmentsPanel';

export function DoctorDashboardScreen() {
  const t = useTranslations('dashboard');
  const {data, isLoading, isError, error, refetch, identity, isDoctor, upcomingAppointments, upcomingIsLoading} =
    useDoctorDashboard();

  return (
    <div className="min-h-screen bg-background text-foreground">
      {/* Main Content Area */}
      <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8">
        {/* State 1: Identity Loading */}
        {identity.status === 'loading' && (
          <div className="space-y-6">
            <div className="h-8 w-48 animate-pulse rounded bg-surface-2" />
            <DashboardStatCards isLoading={true} />
            <TodayAppointmentsTable isLoading={true} />
          </div>
        )}

        {/* State 2: No Identity */}
        {identity.status === 'none' && (
          <div className="rounded-2xl border border-border bg-surface p-8 text-center shadow-xs">
            <div className="mx-auto mb-4 grid size-12 place-items-center rounded-2xl bg-brand-soft text-brand">
              <FiUserCheck className="size-6" />
            </div>
            <h2 className="text-lg font-bold text-foreground">
              {t('states.identityRequiredTitle')}
            </h2>
            <p className="mt-1 text-xs text-muted max-w-md mx-auto">
              {t('states.identityRequired')}
            </p>
          </div>
        )}

        {/* State 3: Logged in but not Doctor */}
        {identity.status === 'ready' && !isDoctor && (
          <div className="rounded-2xl border border-rose-500/20 bg-rose-500/5 p-8 text-center shadow-xs">
            <div className="mx-auto mb-4 grid size-12 place-items-center rounded-2xl bg-rose-500/10 text-rose-500">
              <FiAlertCircle className="size-6" />
            </div>
            <h2 className="text-lg font-bold text-foreground">
              {t('states.forbiddenTitle')}
            </h2>
            <p className="mt-1 text-xs text-muted max-w-md mx-auto">
              {t('states.doctorOnly')}
            </p>
          </div>
        )}

        {/* State 4: Doctor Access Granted */}
        {isDoctor && (
          <div className="space-y-8">
            {/* Greeting Header */}
            <div className="flex flex-wrap items-end justify-between gap-4">
              <div>
                <h1 className="text-2xl font-black text-foreground">
                  {data?.doctorName
                    ? `${t('welcome')} ${data.doctorName}`
                    : t('title')}
                </h1>
                <p className="mt-1 text-xs text-muted">
                  {data?.specialization
                    ? `${data.specialization} · ${t('subtitle')}`
                    : t('subtitle')}
                </p>
              </div>

              <button
                type="button"
                onClick={() => refetch()}
                disabled={isLoading}
                className="inline-flex items-center gap-2 rounded-xl border border-border bg-surface px-3.5 py-2 text-xs font-bold text-foreground transition hover:bg-surface-2 hover:border-brand/40 disabled:opacity-50"
              >
                <FiRefreshCw
                  className={`size-3.5 text-muted ${isLoading ? 'animate-spin' : ''}`}
                />
                <span>{t('refresh')}</span>
              </button>
            </div>

            {/* Error Message */}
            {isError && (
              <div className="flex items-center justify-between gap-4 rounded-2xl border border-rose-500/20 bg-rose-500/5 p-4 text-xs text-rose-600 dark:text-rose-400">
                <div className="flex items-center gap-2.5">
                  <FiAlertCircle className="size-4 shrink-0" />
                  <span>
                    {error?.message || t('states.error')}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => refetch()}
                  className="rounded-lg bg-rose-500/10 px-3 py-1.5 font-bold hover:bg-rose-500/20 transition"
                >
                  {t('states.retry')}
                </button>
              </div>
            )}

            {/* 5 Stat Cards */}
            <DashboardStatCards
              metrics={data?.metrics}
              isLoading={isLoading}
            />

            {/* Today's Appointments Table */}
            <TodayAppointmentsTable
              appointments={data?.todayAppointments}
              isLoading={isLoading}
            />
            <UpcomingAppointmentsPanel
              appointments={upcomingAppointments}
              isLoading={upcomingIsLoading}
            />
          </div>
        )}
      </main>
    </div>
  );
}

