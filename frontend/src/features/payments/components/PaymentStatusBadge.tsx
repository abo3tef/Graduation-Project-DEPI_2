import {useTranslations} from 'next-intl';
import type {PaymentStatus} from '../types/payment.types';

/**
 * Colors come from the project's theme tokens. The app switches themes with
 * `data-theme` on <html> (not the `dark:` variant), so the light-theme
 * overrides use the `[:root[data-theme=light]_&]` variant.
 *
 * `Record<PaymentStatus, string>` makes TypeScript fail here if a status is
 * added to `payment.types.ts` and not styled (PROVISIONAL – Q-01d).
 */
const STATUS_STYLES: Record<PaymentStatus, string> = {
  paid: 'border-brand/30 bg-brand-soft text-brand [:root[data-theme=light]_&]:text-brand-strong',
  partial:
    'border-amber-400/40 bg-amber-400/15 text-amber-300 [:root[data-theme=light]_&]:border-amber-500/40 [:root[data-theme=light]_&]:bg-amber-100 [:root[data-theme=light]_&]:text-amber-800',
  pending:
    'border-border bg-surface-2 text-muted [:root[data-theme=light]_&]:text-foreground'
};

type PaymentStatusBadgeProps = {
  status: PaymentStatus;
  className?: string;
};

export function PaymentStatusBadge({
  status,
  className = ''
}: PaymentStatusBadgeProps) {
  const t = useTranslations('Payments.status');

  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2.5 py-1 text-xs font-bold ${STATUS_STYLES[status]} ${className}`.trim()}
    >
      {/* Decorative dot: the meaning is carried by the text, not by color alone. */}
      <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />
      {t(status)}
    </span>
  );
}