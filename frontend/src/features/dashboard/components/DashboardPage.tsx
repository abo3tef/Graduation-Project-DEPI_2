"use client";

import { useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import {
  FiActivity,
  FiBell,
  FiCalendar,
  FiCheckCircle,
  FiChevronLeft,
  FiChevronRight,
  FiClock,
  FiDollarSign,
  FiFileText,
  FiMoreVertical,
  FiPlus,
  FiSearch,
  FiUserPlus,
  FiUsers,
} from "react-icons/fi";
import { Link } from "@/i18n/navigation";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { notify } from "@/lib/utils/toast";
import {
  doctorAppointments,
  secretaryAppointments,
  type DashboardAppointment,
  type DashboardRole,
  weeklyVisits,
} from "../dashboard-data";

const statusStyles = {
  confirmed: "bg-brand-soft text-brand",
  waiting: "bg-amber-500/15 text-amber-500",
  completed: "bg-blue-500/15 text-blue-500",
  cancelled: "bg-red-500/15 text-red-500",
} as const;

type DashboardPageProps = {
  role: DashboardRole;
};

export function DashboardPage({ role }: DashboardPageProps) {
  const t = useTranslations("Dashboard");
  const pageSize = 4;
  const [appointments, setAppointments] = useState<DashboardAppointment[]>(
    role === "secretary" ? secretaryAppointments : doctorAppointments,
  );
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [openActionId, setOpenActionId] = useState<number | null>(null);

  const filteredAppointments = useMemo(
    () =>
      appointments.filter((appointment) =>
        `${appointment.patient} ${appointment.doctor}`
          .toLowerCase()
          .includes(search.toLowerCase()),
      ),
    [appointments, search],
  );
  const totalPages = Math.max(
    1,
    Math.ceil(filteredAppointments.length / pageSize),
  );
  const visibleAppointments = filteredAppointments.slice(
    (page - 1) * pageSize,
    page * pageSize,
  );

  const updateStatus = (id: number, status: DashboardAppointment["status"]) => {
    setAppointments((current) =>
      current.map((appointment) =>
        appointment.id === id ? { ...appointment, status } : appointment,
      ),
    );
    notify.success(t(`toast.${status}`));
    setOpenActionId(null);
  };

  const handleSearch = (value: string) => {
    setSearch(value);
    setPage(1);
    setOpenActionId(null);
  };

  const stats =
    role === "secretary"
      ? [
          {
            label: t("secretary.stats.todayAppointments"),
            value: "24",
            change: "+12%",
            icon: FiCalendar,
            tone: "text-brand",
          },
          {
            label: t("secretary.stats.waitingPatients"),
            value: "08",
            change: t("common.now"),
            icon: FiClock,
            tone: "text-amber-500",
          },
          {
            label: t("secretary.stats.totalPatients"),
            value: "1,248",
            change: "+8%",
            icon: FiUsers,
            tone: "text-blue-500",
          },
          {
            label: t("secretary.stats.todayRevenue"),
            value: "12,450",
            change: "+18%",
            icon: FiDollarSign,
            tone: "text-purple-500",
          },
        ]
      : [
          {
            label: t("doctor.stats.todayVisits"),
            value: "18",
            change: "+15%",
            icon: FiActivity,
            tone: "text-brand",
          },
          {
            label: t("doctor.stats.completedVisits"),
            value: "12",
            change: t("common.today"),
            icon: FiCheckCircle,
            tone: "text-blue-500",
          },
          {
            label: t("doctor.stats.activePatients"),
            value: "342",
            change: "+6%",
            icon: FiUsers,
            tone: "text-purple-500",
          },
          {
            label: t("doctor.stats.monthRevenue"),
            value: "86,400",
            change: "+22%",
            icon: FiDollarSign,
            tone: "text-amber-500",
          },
        ];

  return (
    <div className="min-h-screen bg-background pb-12">
      <header className="border-b border-border/60 bg-surface/80">
        <div className="mx-auto flex min-h-20 w-[min(1240px,calc(100%-32px))] items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <Link
              href="/"
              className="grid size-10 place-items-center rounded-xl bg-brand text-[#06211f]"
              aria-label="Medilog"
            >
              <FiActivity className="size-5" aria-hidden />
            </Link>
            <div>
              <p className="m-0 text-lg font-black">{t("brand")}</p>
              <p className="m-0 text-xs text-muted">{t("demoLabel")}</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              className="relative grid size-10 place-items-center rounded-full border border-border text-muted transition hover:border-brand hover:text-brand"
              onClick={() => notify.info(t("toast.notifications"))}
              aria-label={t("notifications")}
            >
              <FiBell aria-hidden />
              <span className="absolute right-2 top-2 size-2 rounded-full bg-brand" />
            </button>
            <ThemeToggle />
            <div className="hidden items-center gap-2 rounded-full border border-border bg-background px-3 py-1.5 sm:flex">
              <span className="grid size-8 place-items-center rounded-full bg-brand-soft text-sm font-bold text-brand">
                {role === "secretary" ? "م" : "أ"}
              </span>
              <span className="text-xs font-bold">{t(`${role}.role`)}</span>
            </div>
          </div>
        </div>
      </header>

      <main className="mx-auto w-[min(1240px,calc(100%-32px))] py-8">
        <div className="mb-8 flex flex-col justify-between gap-5 lg:flex-row lg:items-end">
          <div>
            <p className="mb-2 text-sm font-bold text-brand">{t("eyebrow")}</p>
            <h1 className="m-0 text-3xl font-black tracking-tight sm:text-4xl">
              {t(`${role}.title`)}
            </h1>
            <p className="mt-2 max-w-2xl text-sm leading-7 text-muted">
              {t(`${role}.description`)}
            </p>
          </div>
        </div>

        <section
          className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"
          aria-label={t("summary")}
        >
          {stats.map((stat) => {
            const Icon = stat.icon;
            return (
              <article
                key={stat.label}
                className="rounded-2xl border border-border bg-surface p-5 shadow-sm"
              >
                <div className="mb-5 flex items-center justify-between">
                  <span
                    className={`grid size-11 place-items-center rounded-xl bg-background ${stat.tone}`}
                  >
                    <Icon className="size-5" aria-hidden />
                  </span>
                  <span className="text-[11px] font-bold text-brand">
                    {stat.change}
                  </span>
                </div>
                <p className="m-0 text-sm text-muted">{stat.label}</p>
                <strong className="mt-1 block text-2xl font-black tracking-tight">
                  {stat.value}
                  {stat.label.includes("Revenue") ||
                  stat.label.includes("الإيرادات") ? (
                    <span className="ms-1 text-sm font-bold text-muted">
                      {t("currency")}
                    </span>
                  ) : null}
                </strong>
              </article>
            );
          })}
        </section>

        <section className="mt-6 grid gap-6 lg:grid-cols-[1.4fr_1fr]">
          <article className="rounded-2xl border border-border bg-surface p-5">
            <div className="mb-5 flex items-center justify-between gap-3">
              <div>
                <h2 className="m-0 text-lg font-black">
                  {t(`${role}.activityTitle`)}
                </h2>
                <p className="mt-1 text-xs text-muted">{t("today")}</p>
              </div>
              <button
                type="button"
                onClick={() => notify.info(t("toast.exported"))}
                className="rounded-full border border-border px-3 py-2 text-xs font-bold text-muted transition hover:border-brand hover:text-brand"
              >
                {t("export")}
              </button>
            </div>
            <div className="relative h-48 border-b border-s border-border">
              <div className="absolute inset-0 flex items-end justify-between gap-2 px-2">
                {weeklyVisits.map((value, index) => (
                  <div
                    key={index}
                    className="flex h-full flex-1 flex-col items-center justify-end gap-2"
                  >
                    <span className="text-[10px] text-muted">{value}</span>
                    <div
                      className="w-full max-w-10 rounded-t-lg bg-brand/80 transition hover:bg-brand"
                      style={{ height: `${value * 1.65}px` }}
                    />
                    <span className="text-[10px] text-muted">
                      {t(`week.${index}`)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </article>

          <article className="rounded-2xl border border-border bg-surface p-5">
            <div className="mb-5 flex items-center justify-between">
              <div>
                <h2 className="m-0 text-lg font-black">
                  {t("quickActions.title")}
                </h2>
                <p className="mt-1 text-xs text-muted">
                  {t("quickActions.description")}
                </p>
              </div>
              <FiPlus className="text-brand" aria-hidden />
            </div>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-1">
              {(role === "secretary"
                ? [
                    {
                      icon: FiCalendar,
                      label: t("quickActions.book"),
                      message: t("toast.booked"),
                    },
                    {
                      icon: FiUserPlus,
                      label: t("quickActions.patient"),
                      message: t("toast.patientAdded"),
                    },
                    {
                      icon: FiDollarSign,
                      label: t("quickActions.payment"),
                      message: t("toast.paymentAdded"),
                    },
                  ]
                : [
                    {
                      icon: FiFileText,
                      label: t("quickActions.visit"),
                      message: t("toast.visitStarted"),
                    },
                    {
                      icon: FiUsers,
                      label: t("quickActions.patient"),
                      message: t("toast.patientOpened"),
                    },
                    {
                      icon: FiCalendar,
                      label: t("quickActions.schedule"),
                      message: t("toast.scheduleOpened"),
                    },
                  ]
              ).map((action) => {
                const Icon = action.icon;
                return (
                  <button
                    key={action.label}
                    type="button"
                    onClick={() => notify.success(action.message)}
                    className="flex items-center gap-3 rounded-xl border border-border bg-background p-3 text-start text-sm font-bold transition hover:border-brand hover:bg-brand-soft"
                  >
                    <span className="grid size-9 place-items-center rounded-lg bg-brand-soft text-brand">
                      <Icon aria-hidden />
                    </span>
                    {action.label}
                  </button>
                );
              })}
            </div>
          </article>
        </section>

        <section className="mt-6 rounded-2xl border border-border bg-surface p-5">
          <div className="mb-5 flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
            <div>
              <h2 className="m-0 text-lg font-black">
                {t(`${role}.tableTitle`)}
              </h2>
              <p className="mt-1 text-xs text-muted">{t("tableHint")}</p>
            </div>
            <label className="flex items-center gap-2 rounded-xl border border-border bg-background px-3 py-2 text-xs text-muted focus-within:border-brand sm:w-64">
              <FiSearch aria-hidden />
              <span className="sr-only">{t("search")}</span>
              <input
                value={search}
                onChange={(event) => handleSearch(event.target.value)}
                placeholder={t("searchPlaceholder")}
                className="min-w-0 flex-1 bg-transparent outline-none placeholder:text-muted"
              />
            </label>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[680px] text-start text-sm">
              <thead className="border-b border-border text-xs text-muted">
                <tr>
                  {[
                    "time",
                    "patient",
                    "doctor",
                    "type",
                    "status",
                    "actions",
                  ].map((head) => (
                    <th key={head} className="px-3 py-3 font-bold">
                      {t(`table.${head}`)}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {visibleAppointments.map((appointment) => (
                  <tr
                    key={appointment.id}
                    className="transition hover:bg-background/60"
                  >
                    <td className="px-3 py-4 font-black text-brand">
                      {appointment.time}
                    </td>
                    <td className="px-3 py-4 font-bold">
                      {appointment.patient}
                    </td>
                    <td className="px-3 py-4 text-muted">
                      {appointment.doctor}
                    </td>
                    <td className="px-3 py-4 text-muted">{appointment.type}</td>
                    <td className="px-3 py-4">
                      <span
                        className={`rounded-full px-2.5 py-1 text-[11px] font-bold ${statusStyles[appointment.status]}`}
                      >
                        {t(`statuses.${appointment.status}`)}
                      </span>
                    </td>
                    <td className="relative px-3 py-4">
                      <button
                        type="button"
                        onClick={() =>
                          setOpenActionId(
                            openActionId === appointment.id
                              ? null
                              : appointment.id,
                          )
                        }
                        className="grid size-8 place-items-center rounded-lg border border-border text-muted transition hover:border-brand hover:text-brand"
                        aria-label={t("actions.openMenu")}
                        aria-expanded={openActionId === appointment.id}
                      >
                        <FiMoreVertical aria-hidden />
                      </button>
                      {openActionId === appointment.id && (
                        <div className="absolute end-3 top-14 z-10 min-w-40 rounded-xl border border-border bg-surface p-1.5 shadow-xl">
                          <button
                            type="button"
                            onClick={() => {
                              notify.info(t("toast.details"));
                              setOpenActionId(null);
                            }}
                            className="block w-full rounded-lg px-3 py-2 text-start text-xs font-bold transition hover:bg-brand-soft hover:text-brand"
                          >
                            {t("actions.details")}
                          </button>
                          {appointment.status === "waiting" && (
                            <button
                              type="button"
                              onClick={() =>
                                updateStatus(appointment.id, "confirmed")
                              }
                              className="block w-full rounded-lg px-3 py-2 text-start text-xs font-bold transition hover:bg-brand-soft hover:text-brand"
                            >
                              {t("actions.confirm")}
                            </button>
                          )}
                          {appointment.status === "confirmed" && (
                            <button
                              type="button"
                              onClick={() =>
                                updateStatus(
                                  appointment.id,
                                  role === "doctor" ? "completed" : "cancelled",
                                )
                              }
                              className="block w-full rounded-lg px-3 py-2 text-start text-xs font-bold transition hover:bg-brand-soft hover:text-brand"
                            >
                              {role === "doctor"
                                ? t("actions.complete")
                                : t("actions.cancel")}
                            </button>
                          )}
                          {(appointment.status === "confirmed" ||
                            appointment.status === "waiting") && (
                            <button
                              type="button"
                              onClick={() => {
                                notify.success(t("toast.rescheduled"));
                                setOpenActionId(null);
                              }}
                              className="block w-full rounded-lg px-3 py-2 text-start text-xs font-bold transition hover:bg-brand-soft hover:text-brand"
                            >
                              {t("actions.reschedule")}
                            </button>
                          )}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {visibleAppointments.length === 0 && (
              <div className="py-12 text-center text-sm text-muted">
                {t("noResults")}
              </div>
            )}
          </div>
          <div className="mt-5 flex items-center justify-between border-t border-border pt-4">
            <p className="m-0 text-xs text-muted">
              {t("pagination.pageOf", { page, totalPages })}
            </p>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={page === 1}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                className="grid size-9 place-items-center rounded-lg border border-border text-muted transition hover:border-brand hover:text-brand disabled:cursor-not-allowed disabled:opacity-40"
                aria-label={t("pagination.previous")}
              >
                <FiChevronLeft className="rtl:rotate-180" aria-hidden />
              </button>
              <span className="min-w-9 text-center text-xs font-bold text-brand">
                {page}
              </span>
              <button
                type="button"
                disabled={page === totalPages}
                onClick={() =>
                  setPage((current) => Math.min(totalPages, current + 1))
                }
                className="grid size-9 place-items-center rounded-lg border border-border text-muted transition hover:border-brand hover:text-brand disabled:cursor-not-allowed disabled:opacity-40"
                aria-label={t("pagination.next")}
              >
                <FiChevronRight className="rtl:rotate-180" aria-hidden />
              </button>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}
