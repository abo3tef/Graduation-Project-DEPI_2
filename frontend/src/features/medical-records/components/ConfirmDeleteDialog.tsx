'use client';

import {useEffect, useRef} from 'react';
import {FiAlertTriangle} from 'react-icons/fi';
import {MutationAlert} from './MutationAlert';
import {dangerButtonClassName, secondaryButtonClassName} from './form-styles';

type ConfirmDeleteDialogProps = {
  open: boolean;
  title: string;
  description: string;
  confirmLabel: string;
  pendingLabel: string;
  cancelLabel: string;
  isPending: boolean;
  error?: unknown;
  /** Focused after close when the trigger no longer exists (for example, a deleted row). */
  fallbackFocusId?: string;
  onConfirm: () => void;
  onCancel: () => void;
};

const FOCUSABLE_SELECTOR =
  'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Modal confirmation for the permanent medication delete (D-23). It keeps the safe action
 * focused, traps `Tab`, closes on `Esc` (unless a delete is already in flight), returns focus to
 * the trigger on close and states the irreversibility explicitly.
 */
export function ConfirmDeleteDialog({
  open,
  title,
  description,
  confirmLabel,
  pendingLabel,
  cancelLabel,
  isPending,
  error,
  fallbackFocusId,
  onConfirm,
  onCancel
}: ConfirmDeleteDialogProps) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const cancelRef = useRef<HTMLButtonElement>(null);
  const restoreFocusRef = useRef<HTMLElement | null>(null);
  const onCancelRef = useRef(onCancel);
  const isPendingRef = useRef(isPending);

  useEffect(() => {
    onCancelRef.current = onCancel;
  }, [onCancel]);

  useEffect(() => {
    isPendingRef.current = isPending;
  }, [isPending]);

  useEffect(() => {
    if (!open) {
      return;
    }

    restoreFocusRef.current =
      document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    cancelRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault();

        if (!isPendingRef.current) {
          onCancelRef.current();
        }

        return;
      }

      if (event.key !== 'Tab') {
        return;
      }

      const container = dialogRef.current;

      if (!container) {
        return;
      }

      const focusable = Array.from(
        container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)
      );

      if (focusable.length === 0) {
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    }

    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      document.body.style.overflow = previousOverflow;

      const target = restoreFocusRef.current;
      restoreFocusRef.current = null;

      if (target?.isConnected) {
        target.focus();
        return;
      }

      const fallback = fallbackFocusId
        ? document.getElementById(fallbackFocusId)
        : null;
      fallback?.focus();
    };
  }, [open, fallbackFocusId]);

  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/60 p-4">
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirm-delete-title"
        aria-describedby="confirm-delete-description"
        aria-busy={isPending}
        className="w-full max-w-md rounded-2xl border border-border bg-surface p-5 shadow-2xl"
      >
        <h2
          id="confirm-delete-title"
          className="flex items-center gap-2 text-lg font-bold text-danger"
        >
          <FiAlertTriangle aria-hidden className="size-5" />
          {title}
        </h2>

        <p
          id="confirm-delete-description"
          className="mt-2 text-sm leading-relaxed text-muted"
        >
          {description}
        </p>

        {error ? (
          <div className="mt-3">
            <MutationAlert error={error} />
          </div>
        ) : null}

        <div className="mt-5 flex flex-wrap justify-end gap-2">
          <button
            ref={cancelRef}
            type="button"
            onClick={onCancel}
            disabled={isPending}
            className={secondaryButtonClassName}
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={isPending}
            className={dangerButtonClassName}
          >
            {isPending ? pendingLabel : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
