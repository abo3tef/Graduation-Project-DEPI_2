import type {ReactNode} from 'react';

type AuthLayoutProps = {
  children: ReactNode;
  params: Promise<{locale: string}>;
};

/**
 * Shared shell of the signed-out screens: the product mark, the language and theme controls, and
 * one narrow column so the form is readable in both LTR and RTL.
 */
export default async function AuthLayout({children, params}: AuthLayoutProps) {
  await params;

  return (
    <div className="min-h-dvh bg-background">
      <main className="mx-auto w-[min(980px,calc(100%-32px))] px-0 pb-20 pt-10">{children}</main>
    </div>
  );
}
