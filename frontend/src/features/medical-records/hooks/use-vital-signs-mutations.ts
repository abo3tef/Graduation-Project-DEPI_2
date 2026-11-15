'use client';

import {useMutation, useQueryClient} from '@tanstack/react-query';
import {createVitalSigns, updateVitalSigns} from '../api';
import {medicalRecordKeys} from '../keys';
import {readDevToken} from '../use-dev-token';
import {ApiError} from '@/lib/api';
import {notify} from '@/lib/utils/toast';
import type {SaveVitalSignsRequest} from '../types';

type SaveVariables = {payload: SaveVitalSignsRequest; token: string};
type UpdateVariables = SaveVariables & {vitalSignsId: string};
// The UI supplies translated notification text when T3/T5 are implemented.
type Messages = {success: string; error: string};

function requireCurrentToken(token: string): void {
  if (!token.trim() || token !== readDevToken()) {
    throw new ApiError({kind: 'unauthorized', status: 401});
  }
}

function useSaveCallbacks(visitId: string, messages?: Messages) {
  const queryClient = useQueryClient();
  return {
    onSuccess: async (_data: unknown, variables: SaveVariables) => {
      // Invalidate without seeding data from a request completed after an account change.
      await Promise.all([
        queryClient.invalidateQueries({queryKey: medicalRecordKeys.visit(visitId)}),
        queryClient.invalidateQueries({queryKey: medicalRecordKeys.vitalSigns(visitId)})
      ]);
      if (messages && variables.token === readDevToken()) {
        notify.success(messages.success);
      }
    },
    onError: (_error: unknown, variables: SaveVariables) => {
      if (messages && variables.token === readDevToken()) {
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
