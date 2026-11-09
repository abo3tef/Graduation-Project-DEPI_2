"use client";

import { useTransition } from "react";
import { FiGlobe } from "react-icons/fi";
import { useLocale, useTranslations } from "next-intl";
import { usePathname, useRouter } from "@/i18n/navigation";
import { routing, type Locale } from "@/i18n/routing";

export function LocaleSwitcher() {
  const t = useTranslations("LocaleSwitcher");
  const locale = useLocale();
  const pathname = usePathname();
  const router = useRouter();
  const [isPending, startTransition] = useTransition();

  function switchLocale(nextLocale: Locale) {
    if (nextLocale === locale) {
      return;
    }

    startTransition(() => {
      router.replace(pathname, { locale: nextLocale });
    });
  }

  return (
    <div className="flex items-center gap-2 text-muted">
      <FiGlobe className="size-3.5" aria-hidden />
      <div
        role="group"
        aria-label={t("label")}
        className="flex rounded-full border border-border bg-surface p-0.5"
      >
        {routing.locales.map((item) => {
          const isActive = item === locale;

          return (
            <button
              key={item}
              type="button"
              onClick={() => switchLocale(item)}
              disabled={isPending}
              aria-pressed={isActive}
              className={`rounded-full border-0 px-2 py-1 text-[10px] font-bold text-muted transition hover:text-foreground ${
                isActive ? "bg-brand text-[#06211f]" : ""
              } disabled:cursor-wait disabled:opacity-70`}
            >
              {t(item)}
            </button>
          );
        })}
      </div>
    </div>
  );
}
