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
 * the params object in one fixed shape so keys stay deterministic. Actor and
 * credential revision scope medical data without exposing the credential itself.
 */
export const medicalTimelineKeys = {
  all: timelineRoot,
  timeline: (
    patientId: string,
    params: PatientTimelineQueryParams,
    actor: MedicalTimelineActor,
    tokenRevision: number
  ) =>
    [
      ...timelineRoot,
      'timeline',
      actor.role,
      actor.userId,
      patientId,
      tokenRevision,
      params
    ] as const,
  profile: (patientId: string, actor: MedicalTimelineActor, tokenRevision: number) =>
    [
      ...timelineRoot,
      'profile',
      actor.role,
      actor.userId,
      patientId,
      tokenRevision
    ] as const
};
