import {toast} from 'react-toastify';

/**
 * Small, reusable notification API so feature code does not depend directly
 * on the toast library. The container itself is mounted in the app providers.
 */
export const notify = {
  success: (message: string) => toast.success(message),
  error: (message: string) => toast.error(message),
  info: (message: string) => toast.info(message),
  warning: (message: string) => toast.warning(message)
};
