"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import { FiMoon, FiSun } from "react-icons/fi";
import { useTranslations } from "next-intl";

type Theme = "dark" | "light";

function getStoredTheme(): Theme {
  const storedTheme = window.localStorage.getItem("medilog-theme");
  return storedTheme === "light" || storedTheme === "dark"
    ? storedTheme
    : "dark";
}

function subscribeToTheme() {
  return () => {};
}

export function ThemeToggle() {
  const t = useTranslations("Navigation");
  const storedTheme = useSyncExternalStore(
    subscribeToTheme,
    getStoredTheme,
    () => "dark"
  );
  const [selectedTheme, setSelectedTheme] = useState<Theme | null>(null);
  const theme = selectedTheme ?? storedTheme;

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
  }, [theme]);

  function toggleTheme() {
    const nextTheme: Theme = theme === "dark" ? "light" : "dark";
    setSelectedTheme(nextTheme);
    document.documentElement.dataset.theme = nextTheme;
    window.localStorage.setItem("medilog-theme", nextTheme);
  }

  const isDark = theme === "dark";

  return (
    <button
      type="button"
      onClick={toggleTheme}
      suppressHydrationWarning
      aria-label={isDark ? t("lightMode") : t("darkMode")}
      aria-pressed={isDark}
      className="inline-flex min-h-9 items-center justify-center gap-2 rounded-full border border-border px-3 text-xs text-muted transition hover:-translate-y-0.5 hover:border-brand hover:text-brand"
    >
      <span className="grid place-items-center">
        {isDark ? <FiSun aria-hidden /> : <FiMoon aria-hidden />}
      </span>
    </button>
  );
}
