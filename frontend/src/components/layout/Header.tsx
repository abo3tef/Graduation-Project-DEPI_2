import {
  FiActivity,
  FiArrowLeft,
  FiArrowRight,
  FiMenu,
  FiX,
} from "react-icons/fi";
import { useTranslations } from "next-intl";
import { LocaleSwitcher } from "@/components/shared/LocaleSwitcher";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { useState } from "react";
import { Link } from "@/i18n/navigation";

export function Header() {
  const t = useTranslations("Navigation");
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const navItems = ["features", "howItWorks", "security", "contact"] as const;

  return (
    <header className="sticky top-0 z-20 border-b border-border/60 bg-background/85 backdrop-blur-xl">
      <div className="mx-auto flex min-h-[76px] w-[min(1180px,calc(100%-40px))] items-center justify-between gap-6">
        <Link
          href="/"
          className="inline-flex items-center gap-2.5"
          aria-label={t("brand")}
        >
          <span className="grid size-9 place-items-center rounded-[10px] bg-brand text-[#06211f] shadow-[0_8px_26px_#2dd4bf2b]">
            <FiActivity className="size-5 stroke-[2.5]" aria-hidden />
          </span>
          <div className="leading-tight">
            <p className="m-0 text-base font-black">{t("brand")}</p>
            <p className="m-0 mt-px text-[10px] text-muted max-sm:hidden">
              {t("tagline")}
            </p>
          </div>
        </Link>

        <nav
          className={`absolute inset-x-3 top-[66px] flex-col gap-0 rounded-[14px] border border-border bg-surface p-2 shadow-xl md:static md:flex md:flex-row md:items-center md:gap-7 md:border-0 md:bg-transparent md:p-0 md:shadow-none ${isMenuOpen ? "flex" : "hidden"}`}
        >
          {navItems.map((item) => (
            <Link
              key={item}
              className="rounded-lg px-3 py-3 text-xs text-muted transition hover:bg-brand-soft hover:text-brand md:p-0 md:hover:bg-transparent"
              href={`/#${item}`}
              onClick={() => setIsMenuOpen(false)}
            >
              {t(item)}
            </Link>
          ))}
          <Link
            className="rounded-lg px-3 py-3 text-xs font-semibold text-brand transition hover:bg-brand-soft md:p-0 md:hover:bg-transparent"
            href="/medical-records"
            onClick={() => setIsMenuOpen(false)}
          >
            {t("medicalRecords")}
          </Link>
        </nav>

        <div className="flex items-center gap-2">
          <ThemeToggle />
          <LocaleSwitcher />
          <Link
            href="/#contact"
            className="inline-flex min-h-9 items-center justify-center gap-2 rounded-full border border-muted px-4 text-xs text-foreground transition hover:border-brand hover:text-brand max-md:hidden"
          >
            {t("login")}
            <FiArrowLeft className="size-3 ltr:hidden rtl:block" aria-hidden />
            <FiArrowRight
              className="size-3 hidden ltr:block rtl:hidden"
              aria-hidden
            />
          </Link>
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
