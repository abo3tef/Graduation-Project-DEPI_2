import type {ReactNode} from 'react';
import {FiActivity} from 'react-icons/fi';
import {getTranslations} from 'next-intl/server';
import {Link} from '@/i18n/navigation';
import {LocaleSwitcher} from '@/components/shared/LocaleSwitcher';
import {ThemeToggle} from '@/components/layout/ThemeToggle';

type AuthLayoutProps = {
  children: ReactNode;
  params: Promise<{locale: string}>;
};

/**
 * Shared shell of the signed-out screens: the product mark, the language and theme controls, and
 * one narrow column so the form is readable in both LTR and RTL.
 */
export default async function AuthLayout({children, params}: AuthLayoutProps) {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Metadata'});

  return (
    <div className="min-h-dvh bg-background">
      <header className="mx-auto flex w-[min(1100px,calc(100%-32px))] items-center justify-between gap-3 py-6">
        <Link
          href="/"
          className="inline-flex items-center gap-2 text-base font-black tracking-tight text-foreground"
        >
          <FiActivity aria-hidden className="size-5 text-brand" />
          {t('title')}
        </Link>
        <div className="flex items-center gap-2">
          <ThemeToggle />
          <LocaleSwitcher />
        </div>
      </header>
      <main className="mx-auto w-[min(460px,calc(100%-32px))] pb-16">{children}</main>
    </div>
  );
}
