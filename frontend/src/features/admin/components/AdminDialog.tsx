'use client';

import {useEffect, useId, useRef, type ReactNode} from 'react';

type AdminDialogProps = {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: ReactNode;
};

/** Accessible modal built on the native dialog element (focus trap and Escape are browser-native). */
export function AdminDialog({open, onClose, title, description, children}: AdminDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const descriptionId = useId();

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

  return (
    <dialog
      ref={dialogRef}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onCancel={onClose}
      onClick={(event) => {
        if (event.target === dialogRef.current) {
          onClose();
        }
      }}
      className="m-auto max-h-[calc(100dvh-2rem)] w-[min(520px,calc(100%-32px))] overflow-y-auto rounded-2xl border border-border bg-surface p-6 text-foreground shadow-2xl backdrop:bg-black/65 backdrop:backdrop-blur-md sm:p-7"
    >
      <h2 id={titleId} className="text-lg font-bold">
        {title}
      </h2>
      {description ? (
        <p id={descriptionId} className="mt-1 text-sm leading-relaxed text-muted">
          {description}
        </p>
      ) : null}
      <div className="mt-5">{children}</div>
    </dialog>
  );
}
