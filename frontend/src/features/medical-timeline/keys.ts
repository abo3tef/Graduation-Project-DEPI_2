import type {PatientTimelineQueryParams} from './types';
import type {ActingRole} from '@/features/auth';

const timelineRoot = ['medical-timeline'] as const;

type MedicalTimelineActor = {
  userId: string | null;
  role: ActingRole | null;
};

/**
 * Query keys for the medical-timeline feature. The timeline key embeds the filter
 * and paging values so every combination is cached separately; the hook builds
 * the params object in one fixed shape so keys stay deterministic. The acting
 * identity scopes medical data, so a different signed-in user can never read the
 * previous user's cached entries (F6-C2 session store).
 */
export const medicalTimelineKeys = {
  all: timelineRoot,
  timeline: (
    patientId: string,
    params: PatientTimelineQueryParams,
    actor: MedicalTimelineActor
  ) => [...timelineRoot, 'timeline', actor.role, actor.userId, patientId, params] as const,
  profile: (patientId: string, actor: MedicalTimelineActor) =>
    [...timelineRoot, 'profile', actor.role, actor.userId, patientId] as const
};
