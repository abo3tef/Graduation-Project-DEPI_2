import {AppointmentsAppBar} from '@/features/appointments';

export default function AppointmentsLayout({children}: {children: React.ReactNode}) {
  return (
    <>
      <AppointmentsAppBar />
      <main className="mx-auto w-[min(1180px,calc(100%-40px))] py-8">{children}</main>
    </>
  );
}
