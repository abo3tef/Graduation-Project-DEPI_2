'use client';

import {useEffect, useId, useRef} from 'react';
import type {MouseEvent, ReactNode, SyntheticEvent} from 'react';
import {FiX} from 'react-icons/fi';

type DialogShellProps = {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  /** Accessible name of the close (X) button. */
  closeLabel: string;
  /** While true, the dialog cannot be dismissed (e.g. a request is in flight). */
  isBusy?: boolean;
  children: ReactNode;
};

/**
 * Modal dialog built on the native <dialog> element, so the browser provides
 * the focus trap, Escape key, top-layer stacking and focus restoration.
 *
 * - Mobile: a bottom sheet. From `sm` up: a centered card.
 * - Children are mounted only while open, so forms start clean every time.
 */
export function DialogShell({
  open,
  onClose,
  title,
  description,
  closeLabel,
  isBusy = false,
  children
}: DialogShellProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

  // Keep the native dialog in sync with the `open` prop.
  useEffect(() => {
    const dialog = dialogRef.current;

    if (!dialog) {
      return;
    }

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  // Lock page scroll behind the dialog.
  useEffect(() => {
    if (!open) {
      return;
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    return () => {
      document.body.style.overflow = previousOverflow;
    };
  }, [open]);

  // Escape key: block it while busy.
  function handleCancel(event: SyntheticEvent<HTMLDialogElement>) {
    if (isBusy) {
      event.preventDefault();
    }
  }

  // Fires after the dialog closed itself (Escape). Ignore our own close() call.
  function handleClose() {
    if (open) {
      onClose();
    }
  }

  // The dialog has no padding, so a click that lands on it is a backdrop click.
  function handleBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === event.currentTarget && !isBusy) {
      onClose();
    }
  }

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onCancel={handleCancel}
      onClose={handleClose}
      onClick={handleBackdropClick}
      className="m-0 mb-0 mt-auto w-full max-w-full rounded-t-3xl border border-border bg-surface p-0 text-foreground shadow-2xl backdrop:bg-black/60 backdrop:backdrop-blur-sm sm:m-auto sm:w-[calc(100%-2rem)] sm:max-w-lg sm:rounded-2xl"
    >
      <div className="flex max-h-[90dvh] flex-col">
        <header className="flex items-start justify-between gap-4 border-b border-border px-5 py-4">
          <div className="min-w-0">
            <h2 id={titleId} className="text-lg font-bold text-foreground">
              {title}
            </h2>
            {description ? (
              <p id={descriptionId} className="mt-1 text-sm text-muted">
                {description}
              </p>
            ) : null}
          </div>

          <button
            type="button"
            onClick={onClose}
            disabled={isBusy}
            aria-label={closeLabel}
            className="-me-2 -mt-1 inline-flex size-11 shrink-0 items-center justify-center rounded-xl text-muted transition-colors hover:bg-surface-2 hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand disabled:cursor-not-allowed disabled:opacity-40"
          >
            <FiX aria-hidden="true" className="size-5" />
          </button>
        </header>

        <div className="overflow-y-auto overscroll-contain px-5 py-4">
          {open ? children : null}
        </div>
      </div>
    </dialog>
  );
}
