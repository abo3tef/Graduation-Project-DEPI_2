'use client';

import {useTranslations} from 'next-intl';
import {SkeletonRows} from './primitives/Skeleton';

/**
 * Static fallback for the appointment screens. They read the current clinic time
 * while rendering, which Next.js `cacheComponents` does not allow during
 * prerendering, so each route renders them behind `<Suspense>` with this
 * fallback and the real screen is streamed in per request.
 */
export function PageFallback() {
  const t = useTranslations('Appointments.states');

  return <SkeletonRows label={t('loading')} />;
}
