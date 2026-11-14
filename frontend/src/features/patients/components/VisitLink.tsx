'use client';

import {FiExternalLink} from 'react-icons/fi';
import {Link} from '@/i18n/navigation';
import {isGuid} from '../validation';

type VisitLinkProps = {
  /** `null` for an unlinked record; a GUID for a visit-linked record. */
  visitId: string | null;
  /** Already-translated link text from the calling section's namespace. */
  linkLabel: string;
  /** Already-translated unlinked badge text from the calling section. */
  unlinkedLabel: string;
};

/**
 * The F4 visit-detail link for a visit-linked note/lab result, or the
 * "unlinked" badge when the record has no visit. A non-GUID `visitId`
 * (a contract violation) renders unlinked rather than a broken link.
 */
export function VisitLink({visitId, linkLabel, unlinkedLabel}: VisitLinkProps) {
  if (visitId === null || !isGuid(visitId)) {
    return (
      <span
        className="inline-flex items-center rounded-full border border-border px-2.5 py-0.5 text-xs font-bold text-muted"
        data-testid="unlinked-badge"
      >
        {unlinkedLabel}
      </span>
    );
  }

  return (
    <Link
      href={`/medical-records/visits/${visitId}`}
      className="inline-flex items-center gap-2 rounded-full border border-brand/50 px-3 py-1.5 text-xs font-bold text-brand transition-colors hover:bg-brand-soft focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-brand"
      data-testid="visit-link"
    >
      <FiExternalLink aria-hidden="true" className="size-3.5" />
      {linkLabel}
    </Link>
  );
}
