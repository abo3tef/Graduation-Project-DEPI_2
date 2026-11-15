import {render, screen, within} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {NextIntlClientProvider} from 'next-intl';
import type {ReactNode} from 'react';
import {describe, expect, it, vi} from 'vitest';
import arMessages from '@/messages/ar.json';
import enMessages from '@/messages/en.json';
import type {PatientTimelineEventResponse} from '../types';
import {TimelineEventList} from './TimelineEventList';

vi.mock('@/i18n/navigation', () => ({
  Link: ({
    href,
    className,
    children
  }: {
    href: string;
    className?: string;
    children: ReactNode;
  }) => (
    <a href={href} className={className}>
      {children}
    </a>
  )
}));

const MESSAGES = {en: enMessages, ar: arMessages} as const;

/** Renders with the real translations (a missing key fails the test). */
function renderWithMessages(ui: React.ReactElement, locale: 'en' | 'ar' = 'en') {
  return render(
    <NextIntlClientProvider
      locale={locale}
      messages={MESSAGES[locale]}
      onError={(error) => {
        throw error;
      }}
    >
      {ui}
    </NextIntlClientProvider>
  );
}

/** The §3.3 example page, one event per type, in the order the API returns. */
function makeEvent(overrides: Partial<PatientTimelineEventResponse>): PatientTimelineEventResponse {
  return {
    id: 'e0000000-0000-4000-8000-000000000001',
    type: 'Visit',
    occurredAt: '2026-11-14T10:00:00+00:00',
    title: 'Medical Visit',
    summary: null,
    relatedVisitId: null,
    ...overrides
  };
}

const API_ORDER: readonly PatientTimelineEventResponse[] = [
  makeEvent({id: 'e0000000-0000-4000-8000-000000000001', type: 'MedicalNote', title: 'Clinical Note', summary: 'Follow up in one week.'}),
  makeEvent({id: 'e0000000-0000-4000-8000-000000000002', type: 'Medication', title: 'Amoxicillin', summary: '500 mg · Every 8 hours'}),
  makeEvent({id: 'e0000000-0000-4000-8000-000000000003', type: 'Visit', title: 'Medical Visit'}),
  makeEvent({id: 'e0000000-0000-4000-8000-000000000004', type: 'Diagnosis', title: 'Tension headache', summary: 'Pressure-like pain'})
];

describe('TimelineEventList', () => {
  it('renders events in the exact API order and never re-sorts them', () => {
    renderWithMessages(<TimelineEventList events={API_ORDER} />);

    const titles = screen
      .getAllByRole('button')
      .map((button) => button.getAttribute('aria-label'));

    // API order (§3.3): note first, then medication/visit tied at one instant
    // with the medication winning the type tie-break, then the diagnosis.
    expect(titles).toEqual([
      'Details of Clinical Note',
      'Details of Amoxicillin',
      'Details of Medical Visit',
      'Details of Tension headache'
    ]);
  });

  it.each([
    ['Visit', 'Visit'],
    ['Diagnosis', 'Diagnosis'],
    ['Medication', 'Medication'],
    ['MedicalNote', 'Clinical note']
  ] as const)('labels a %s event with its type chip', (type, label) => {
    renderWithMessages(<TimelineEventList events={[makeEvent({type})]} />);

    expect(screen.getAllByText(label).length).toBeGreaterThan(0);
  });

  it('renders every type label in Arabic as well', () => {
    renderWithMessages(<TimelineEventList events={API_ORDER} />, 'ar');

    for (const label of ['زيارة', 'تشخيص', 'دواء', 'ملاحظة سريرية']) {
      expect(screen.getAllByText(label).length).toBeGreaterThan(0);
    }
  });

  it('opens event details from the keyboard and displays the available event fields', async () => {
    const user = userEvent.setup();
    const event = makeEvent({
      type: 'Diagnosis',
      title: 'Tension headache',
      summary: 'Pressure-like pain',
      relatedVisitId: null
    });
    renderWithMessages(<TimelineEventList events={[event]} />);

    const toggle = screen.getByRole('button', {name: 'Details of Tension headache'});
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.tab();
    expect(toggle).toHaveFocus();
    await user.keyboard('{Enter}');

    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    const details = screen.getByRole('region');
    expect(within(details).getByText('Event type')).toBeInTheDocument();
    expect(within(details).getByText('Diagnosis')).toBeInTheDocument();
    expect(within(details).getByText('Title')).toBeInTheDocument();
    expect(within(details).getByText('Tension headache')).toBeInTheDocument();
    expect(within(details).getByText('Occurred at')).toBeInTheDocument();
    expect(details.querySelector('time')).toHaveAttribute('dateTime', event.occurredAt);
    expect(within(details).getByText('Summary')).toBeInTheDocument();
    expect(within(details).getByText('Pressure-like pain')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Open related visit'})).not.toBeInTheDocument();
  });

  it('shows the related visit ID and links to the existing visit route', async () => {
    const user = userEvent.setup();
    const relatedVisitId = '9c1b0000-0000-4000-8000-000000000001';
    renderWithMessages(
      <TimelineEventList events={[makeEvent({relatedVisitId})]} />
    );

    await user.click(screen.getByRole('button', {name: 'Details of Medical Visit'}));

    expect(screen.getByText(relatedVisitId)).toHaveAttribute('dir', 'ltr');
    expect(screen.getByRole('link', {name: 'Open related visit'})).toHaveAttribute(
      'href',
      `/medical-records/visits/${relatedVisitId}`
    );
  });

  it('renders expanded event details and related visit navigation in Arabic', async () => {
    const user = userEvent.setup();
    renderWithMessages(
      <TimelineEventList
        events={[
          makeEvent({
            type: 'Diagnosis',
            title: 'صداع',
            summary: 'ألم مستمر',
            relatedVisitId: '9c1b0000-0000-4000-8000-000000000001'
          })
        ]}
      />,
      'ar'
    );

    await user.click(screen.getByRole('button', {name: 'تفاصيل صداع'}));

    const details = screen.getByRole('region');
    expect(within(details).getByText('نوع الحدث')).toBeInTheDocument();
    expect(within(details).getByText('العنوان')).toBeInTheDocument();
    expect(within(details).getByText('وقت الحدث')).toBeInTheDocument();
    expect(within(details).getByText('الملخص')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'فتح الزيارة المرتبطة'})).toBeInTheDocument();
  });

  it('uses the translated placeholder for a null summary', async () => {
    const user = userEvent.setup();
    renderWithMessages(<TimelineEventList events={[makeEvent({summary: null})]} />);

    await user.click(screen.getByRole('button', {name: 'Details of Medical Visit'}));

    expect(screen.getByText('Not provided')).toBeInTheDocument();
  });
});
