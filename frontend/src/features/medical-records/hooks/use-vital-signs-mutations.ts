'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {createVitalSigns, updateVitalSigns} from '../api';
import {medicalRecordKeys} from '../keys';
import {readCredentialScope} from '../credential-scope';
import {readMedicalToken} from '../use-medical-credentials';
import {ApiError} from '@/lib/api';
import {notify} from '@/lib/utils/toast';
import type {SaveVitalSignsRequest} from '../types';

type SaveVariables = {payload: SaveVitalSignsRequest; token: string};
type UpdateVariables = SaveVariables & {vitalSignsId: string};
// The UI supplies translated notification text when T3/T5 are implemented.
type Messages = {success: string; error: string};

function requireCurrentToken(token: string): void {
  if (!token.trim() || token !== readMedicalToken()) {
    throw new ApiError({kind: 'unauthorized', status: 401});
  }
}

function useSaveCallbacks(visitId: string, messages?: Messages) {
  const queryClient = useQueryClient();
  return {
    onMutate: () => readCredentialScope(),
    onSuccess: async (_data: unknown, variables: SaveVariables, scope: number | undefined) => {
      // Invalidate without seeding data from a request completed after an account change.
      await Promise.all([
        queryClient.invalidateQueries({queryKey: medicalRecordKeys.visit(visitId)}),
        queryClient.invalidateQueries({queryKey: medicalRecordKeys.vitalSigns(visitId)})
      ]);
      if (messages && scope === readCredentialScope() && variables.token === readMedicalToken()) {
        notify.success(messages.success);
      }
    },
    onError: (_error: unknown, variables: SaveVariables, scope: number | undefined) => {
      if (messages && scope === readCredentialScope() && variables.token === readMedicalToken()) {
        notify.error(messages.error);
      }
    }
  };
}

export function useCreateVitalSignsMutation(visitId: string, messages?: Messages) {
  const callbacks = useSaveCallbacks(visitId, messages);
  return useMutation({
    mutationFn: ({payload, token}: SaveVariables) => {
      requireCurrentToken(token);
      return createVitalSigns(visitId, payload, token);
    },
    ...callbacks
  });
}

export function useUpdateVitalSignsMutation(visitId: string, messages?: Messages) {
  const callbacks = useSaveCallbacks(visitId, messages);
  return useMutation({
    mutationFn: ({vitalSignsId, payload, token}: UpdateVariables) => {
      requireCurrentToken(token);
      return updateVitalSigns(visitId, vitalSignsId, payload, token);
    },
    ...callbacks
  });
}
