'use client';

import {
  FiActivity,
  FiCalendar,
  FiCreditCard,
  FiFileText,
  FiGrid,
  FiHome,
  FiMenu,
  FiShield,
  FiUsers,
  FiX,
} from "react-icons/fi";
import { useTranslations } from "next-intl";
import { LocaleSwitcher } from "@/components/shared/LocaleSwitcher";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { useState } from "react";
import {Link, usePathname} from "@/i18n/navigation";
import { SessionBadge, useActingIdentity } from "@/features/auth";

export function Header() {
  const t = useTranslations("Navigation");
  const identity = useActingIdentity();
  const pathname = usePathname();
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const navItems = ["features", "howItWorks", "security", "contact"] as const;
  const role = identity.status === "ready" ? identity.role : null;
  const workspaceItems = role === 'Admin'
    ? [{href: '/admin', label: 'admin', icon: FiShield}]
    : role === 'Doctor'
      ? [
          {href: '/dashboard', label: 'dashboard', icon: FiGrid},
          {href: '/appointments', label: 'appointments', icon: FiCalendar},
          {href: '/patients', label: 'patients', icon: FiUsers},
          {href: '/payments', label: 'payments', icon: FiCreditCard},
          {href: '/medical-records', label: 'medicalRecords', icon: FiFileText}
        ]
      : role === 'Secretary'
        ? [
            {href: '/appointments', label: 'appointments', icon: FiCalendar},
            {href: '/patients', label: 'patients', icon: FiUsers},
            {href: '/payments', label: 'payments', icon: FiCreditCard}
          ]
        : [];

  return (
    <header className="sticky top-0 z-20 border-b border-border/60 bg-background/85 backdrop-blur-xl">
      <div className="relative mx-auto flex min-h-[76px] w-[min(1180px,calc(100%-24px))] items-center justify-between gap-3 sm:w-[min(1180px,calc(100%-40px))] md:gap-6">
        <Link
          href="/"
          className="inline-flex items-center gap-2.5"
          aria-label={t("brand")}
        >
          <span className="grid size-9 place-items-center rounded-[10px] bg-brand text-[#06211f] shadow-[0_8px_26px_#2dd4bf2b]">
            <FiActivity className="size-5 stroke-[2.5]" aria-hidden />
          </span>
          <div className="leading-tight max-sm:hidden">
            <p className="m-0 text-base font-black">{t("brand")}</p>
            <p className="m-0 mt-px text-[10px] text-muted max-sm:hidden">
              {t("tagline")}
            </p>
          </div>
        </Link>

        <nav
          className={`absolute inset-x-0 top-[calc(100%+8px)] z-30 max-h-[calc(100dvh-96px)] flex-col gap-1 overflow-y-auto rounded-2xl border border-border bg-surface p-2 shadow-2xl md:static md:flex md:max-h-none md:flex-row md:items-center md:gap-5 md:overflow-visible md:border-0 md:bg-transparent md:p-0 md:shadow-none ${isMenuOpen ? "flex" : "hidden"}`}
        >
          {role === null && navItems.map((item) => (
            <Link
              key={item}
              className="rounded-lg px-3 py-3 text-xs text-muted transition hover:bg-brand-soft hover:text-brand md:p-0 md:hover:bg-transparent"
              href={`/#${item}`}
              onClick={() => setIsMenuOpen(false)}
            >
              {t(item)}
            </Link>
          ))}
          {role !== null && <Link
            href="/"
            className="rounded-lg px-3 py-3 text-xs font-semibold text-muted transition hover:bg-brand-soft hover:text-brand md:p-0 md:hover:bg-transparent"
            onClick={() => setIsMenuOpen(false)}
          >
            <FiHome className="me-1 inline size-3.5" aria-hidden />
            {t("brand")}
          </Link>}
          {workspaceItems.map((item) => {
            const Icon = item.icon;
            const current = pathname === item.href || pathname.startsWith(`${item.href}/`);
            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={current ? 'page' : undefined}
                className={`rounded-lg px-3 py-3 text-xs font-semibold transition md:p-0 md:hover:bg-transparent ${
                  current ? 'bg-brand-soft text-brand md:bg-transparent' : 'text-muted hover:bg-brand-soft hover:text-brand'
                }`}
                onClick={() => setIsMenuOpen(false)}
              >
                <Icon className="me-1 inline size-3.5" aria-hidden />
                {t(item.label)}
              </Link>
            );
          })}
          <div className="mt-2 flex items-center gap-2 border-t border-border pt-2 md:hidden">
            <ThemeToggle />
            <LocaleSwitcher />
          </div>
        </nav>

        <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
          <div className="hidden items-center gap-2 md:flex">
            <ThemeToggle />
            <LocaleSwitcher />
          </div>
          <SessionBadge />
          <button
            type="button"
            className="grid size-9 place-items-center border-0 bg-transparent text-foreground md:hidden"
            onClick={() => setIsMenuOpen((open) => !open)}
            aria-label={isMenuOpen ? t("closeMenu") : t("openMenu")}
            aria-expanded={isMenuOpen}
          >
            {isMenuOpen ? <FiX aria-hidden /> : <FiMenu aria-hidden />}
          </button>
        </div>
      </div>
    </header>
  );
}
