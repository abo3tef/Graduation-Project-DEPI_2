'use client';

import {useTranslations} from 'next-intl';
import {
  FiCalendar,
  FiCheckCircle,
  FiCreditCard,
  FiFileText,
  FiUsers
} from 'react-icons/fi';
import type {DoctorDashboardMetrics} from '../types/dashboard.types';

type DashboardStatCardsProps = {
  metrics?: DoctorDashboardMetrics;
  isLoading?: boolean;
};

export function DashboardStatCards({
  metrics,
  isLoading = false
}: DashboardStatCardsProps) {
  const t = useTranslations('dashboard');

  if (isLoading || !metrics) {
    return (
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
        {Array.from({length: 5}).map((_, idx) => (
          <div
            key={idx}
            className="flex animate-pulse flex-col justify-between rounded-2xl border border-border bg-surface p-5 shadow-xs"
          >
            <div className="flex items-center justify-between gap-3">
              <div className="h-4 w-24 rounded-md bg-surface-2" />
              <div className="size-10 rounded-xl bg-surface-2" />
            </div>
            <div className="mt-4 h-8 w-16 rounded-md bg-surface-2" />
          </div>
        ))}
      </div>
    );
  }

  const cards = [
    {
      id: 'todayAppointments',
      label: t('stats.todayAppointments'),
      value: metrics.todayAppointmentsCount.toLocaleString(),
      isUnavailable: false,
      icon: FiCalendar,
      accentClass: 'bg-brand/10 text-brand'
    },
    {
      id: 'todayVisits',
      label: t('stats.todayVisits'),
      value: metrics.todayVisitsCount.toLocaleString(),
      isUnavailable: false,
      icon: FiCheckCircle,
      accentClass: 'bg-emerald-500/10 text-emerald-500'
    },
    {
      id: 'totalVisits',
      label: t('stats.totalVisits'),
      value: metrics.totalVisitsCount.toLocaleString(),
      isUnavailable: false,
      icon: FiFileText,
      accentClass: 'bg-sky-500/10 text-sky-500'
    },
    {
      id: 'totalPatients',
      label: t('stats.totalPatients'),
      value: metrics.totalPatientsCount.toLocaleString(),
      isUnavailable: false,
      icon: FiUsers,
      accentClass: 'bg-indigo-500/10 text-indigo-500'
    },
    {
      id: 'totalRevenue',
      label: t('stats.revenue'),
      value:
        metrics.totalRevenue !== null
          ? `${metrics.totalRevenue.toLocaleString()}`
          : t('revenueUnavailable'),
      isUnavailable: metrics.totalRevenue === null,
      hint: metrics.totalRevenue === null ? t('revenueUnavailableHint') : undefined,
      icon: FiCreditCard,
      accentClass: 'bg-amber-500/10 text-amber-500'
    }
  ];

  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
      {cards.map((card) => {
        const Icon = card.icon;

        return (
          <div
            key={card.id}
            className="flex flex-col justify-between rounded-2xl border border-border bg-surface p-5 shadow-xs transition hover:border-brand/40"
          >
            <div className="flex items-start justify-between gap-3">
              <span className="text-xs font-semibold text-muted">
                {card.label}
              </span>
              <span
                className={`grid size-10 shrink-0 place-items-center rounded-xl ${card.accentClass}`}
                aria-hidden
              >
                <Icon className="size-5" />
              </span>
            </div>

            <div className="mt-3">
              {card.isUnavailable ? (
                <div className="flex flex-col gap-1">
                  <span className="inline-flex items-center rounded-lg bg-surface-2 px-2.5 py-1 text-xs font-bold text-muted">
                    {card.value}
                  </span>
                  {card.hint && (
                    <span className="text-[11px] text-muted/80">
                      {card.hint}
                    </span>
                  )}
                </div>
              ) : (
                <span className="text-2xl font-black tracking-tight text-foreground">
                  {card.value}
                </span>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

