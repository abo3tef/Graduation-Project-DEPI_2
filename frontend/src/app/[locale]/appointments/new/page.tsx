import type {Metadata} from 'next';
import {getTranslations} from 'next-intl/server';
import {Suspense} from 'react';
import {BookingForm, PageFallback} from '@/features/appointments';

type PageProps = {
  params: Promise<{locale: string}>;
};

export async function generateMetadata({params}: PageProps): Promise<Metadata> {
  const {locale} = await params;
  const t = await getTranslations({locale, namespace: 'Appointments.meta'});

  return {title: t('newTitle')};
}

export default function NewAppointmentRoute() {
  // The form reads the current clinic time, so it is streamed behind Suspense.
  return (
    <Suspense fallback={<PageFallback />}>
      <BookingForm />
    </Suspense>
  );
}
