import type {ReactNode} from 'react';
import {RequireRole} from '@/features/auth';

type MedicalRecordsLayoutProps = {
  children: ReactNode;
  params: Promise<{locale: string}>;
};

export default async function MedicalRecordsLayout({
  children,
  params
}: MedicalRecordsLayoutProps) {
  await params;

  return (
    <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8 sm:py-12">
      <RequireRole roles={['Doctor']}>{children}</RequireRole>
    </main>
  );
}
