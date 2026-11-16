'use client';

import {useQuery} from '@tanstack/react-query';
import {ApiError} from '@/lib/api';
import {DEFAULT_PAGE_SIZE} from '@/types/paging';
import {listVitalSigns} from '../api';
import {medicalRecordKeys} from '../keys';
import {useMedicalCredentials} from '../use-medical-credentials';
import {isGuid} from '../validation';

export function useVitalSigns(visitId: string, pageNumber = 1, pageSize = DEFAULT_PAGE_SIZE) {
  const {token, isReady, credentialScope} = useMedicalCredentials();

  return useQuery({
    queryKey: medicalRecordKeys.vitalSignsPage(visitId, credentialScope, pageNumber, pageSize),
    queryFn: ({signal}) => {
      if (!isReady || !token?.trim()) {
        throw new ApiError({kind: 'unauthorized', status: 401});
      }
      return listVitalSigns(visitId, token, pageNumber, pageSize, signal);
    },
    enabled: isReady && Boolean(token?.trim()) && isGuid(visitId),
    // Never show a previous page/identity while the new query is loading.
    placeholderData: undefined,
    gcTime: 0,
    retry: (failureCount, error) => {
      if (error instanceof ApiError &&
          ['unauthorized', 'forbidden', 'not-found', 'validation', 'conflict'].includes(error.kind)) {
        return false;
      }
      return failureCount < 1;
    }
  });
}
