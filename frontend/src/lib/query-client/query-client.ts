import {QueryClient} from '@tanstack/react-query';

/**
 * Centralized React Query configuration shared by the whole application.
 * Query keys and feature-specific options live with their feature module.
 */
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 60 * 1000,
        gcTime: 5 * 60 * 1000,
        retry: 1,
        refetchOnWindowFocus: false
      },
      mutations: {
        retry: 0
      }
    }
  });
}
