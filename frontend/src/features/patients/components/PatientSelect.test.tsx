import {screen, waitFor} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {useState} from 'react';
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {resetAuthHeadersProviders} from '@/lib/api';
import {actAs, DOCTOR_ID, FakeApi, PATIENT_OPTIONS, problem, renderWithProviders} from '../test/test-utils';
import type {PatientOption} from '../types/patient.types';
import {PatientSelect, type PatientSelectProps} from './PatientSelect';

vi.mock('@/i18n/navigation', () => ({
  Link: ({href, children}: {href: string; children?: React.ReactNode}) => <a href={href}>{children}</a>,
  useRouter: () => ({push: vi.fn()}),
  usePathname: () => '/'
}));

const LOOKUP = '/api/patients/lookup';
let api: FakeApi;
const submitted = vi.fn();

beforeEach(() => {
  api = new FakeApi();
  api.install();
  api.on('GET', LOOKUP, (call) => {
    const text = (call.query.get('search') ?? '').toLowerCase();

    return {body: PATIENT_OPTIONS.filter((patient) => patient.fullName.toLowerCase().includes(text))};
  });
  actAs('Doctor');
});

afterEach(() => {
  resetAuthHeadersProviders();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  submitted.mockReset();
});

function Harness(props: Partial<PatientSelectProps> & {onPick?: (patient: PatientOption | null) => void}) {
  const [value, setValue] = useState<PatientOption | null>(null);

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        submitted();
      }}
    >
      <label htmlFor="picker">Patient</label>
      <PatientSelect
        id="picker"
        value={value}
        onChange={(next) => {
          setValue(next);
          props.onPick?.(next);
        }}
        {...props}
      />
    </form>
  );
}

const input = () => screen.getByRole('combobox', {name: 'Patient'});

describe('PatientSelect', () => {
  it('asks for at least two characters before it searches', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    expect(screen.getByText('Type at least 2 characters to search.')).toBeInTheDocument();
    await user.type(input(), 'm');

    expect(api.calls).toHaveLength(0);
  });

  it('searches the lookup endpoint (id, name, phone only) and picks with the keyboard without submitting the form', async () => {
    const onPick = vi.fn();
    const user = userEvent.setup();
    renderWithProviders(<Harness onPick={onPick} />);

    await user.type(input(), 'mo');
    expect(await screen.findByRole('option', {name: /Mona Said/})).toBeInTheDocument();
    expect(screen.getAllByRole('option')).toHaveLength(2);
    await user.keyboard('{ArrowDown}{ArrowDown}{ArrowUp}{Enter}');

    await waitFor(() => expect(input()).toHaveValue('Mona Said'));
    expect(onPick).toHaveBeenCalledWith(PATIENT_OPTIONS[0]);
    expect(submitted).not.toHaveBeenCalled();
    expect(input()).toHaveAttribute('readonly');
    expect(screen.getByText('01012345601')).toBeInTheDocument();

    const call = api.callsTo('GET', LOOKUP).at(-1);
    expect(call?.query.get('search')).toBe('mo');
    expect(call?.query.get('limit')).toBe('10');
    expect(call?.query.has('doctorId')).toBe(false);
  });

  it('wires the combobox ARIA state to the active option and closes on Escape', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.type(input(), 'mo');
    await screen.findAllByRole('option');
    await user.keyboard('{ArrowDown}');

    expect(input()).toHaveAttribute('aria-expanded', 'true');
    const active = screen.getAllByRole('option')[0];
    expect(input()).toHaveAttribute('aria-activedescendant', active?.id);
    expect(active).toHaveAttribute('aria-selected', 'true');

    await user.keyboard('{Escape}');

    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(input()).toHaveAttribute('aria-expanded', 'false');
  });

  it('picks with the mouse and lets the user change the selection', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.type(input(), 'mo');
    await user.click(await screen.findByRole('option', {name: /Mona Hassan/}));
    await waitFor(() => expect(input()).toHaveValue('Mona Hassan'));

    await user.click(screen.getByRole('button', {name: 'Change'}));

    expect(input()).toHaveValue('');
    expect(input()).not.toHaveAttribute('readonly');
    expect(input()).toHaveFocus();
  });

  it('says so when nothing matches', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.type(input(), 'zzz');

    expect(await screen.findByText('No matching patients.')).toBeInTheDocument();
  });

  it('sends the chosen doctor to narrow the search, and stays disabled until one is chosen when required', async () => {
    function DoctorHarness() {
      const [doctorId, setDoctorId] = useState('');

      return (
        <>
          <button type="button" onClick={() => setDoctorId(DOCTOR_ID)}>
            Pick doctor
          </button>
          <Harness requireDoctor doctorId={doctorId} />
        </>
      );
    }

    const user = userEvent.setup();
    renderWithProviders(<DoctorHarness />);

    expect(screen.getByText('Select a doctor first.')).toBeInTheDocument();
    expect(input()).toBeDisabled();
    expect(api.calls).toHaveLength(0);

    await user.click(screen.getByRole('button', {name: 'Pick doctor'}));
    await waitFor(() => expect(input()).toBeEnabled());
    await user.type(input(), 'mo');

    await screen.findAllByRole('option');
    expect(api.callsTo('GET', LOOKUP).at(-1)?.query.get('doctorId')).toBe(DOCTOR_ID);
  });

  it('shows a translated message when the lookup fails', async () => {
    api.on('GET', LOOKUP, problem(500, 'server.error'));
    const user = userEvent.setup();
    renderWithProviders(<Harness />);

    await user.type(input(), 'mo');

    expect(
      await screen.findByText('Something went wrong on the server. Try again later.', {}, {timeout: 4000})
    ).toBeInTheDocument();
    expect(screen.queryByText(/English server text/)).not.toBeInTheDocument();
  }, 10000);

  it('works for a secretary and sends no request without a session', async () => {
    actAs('Secretary');
    const user = userEvent.setup();
    const {unmount} = renderWithProviders(<Harness />);
    await user.type(input(), 'mo');
    expect(await screen.findAllByRole('option')).toHaveLength(2);
    unmount();

    api.calls.length = 0;
    actAs(null);
    renderWithProviders(<Harness />);
    await userEvent.setup().type(input(), 'mo');

    expect(api.calls).toHaveLength(0);
  });

  it('is labelled in Arabic and renders names direction-neutral and phones left-to-right', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness />, 'ar');

    expect(screen.getByText('اكتب # أحرف'.replace('# أحرف', 'حرفين على الأقل للبحث.'))).toBeInTheDocument();
    await user.type(input(), 'mo');
    const option = await screen.findByRole('option', {name: /Mona Said/});

    expect(option.querySelector('span[dir="auto"]')).not.toBeNull();
    expect(option.querySelector('span[dir="ltr"]')).toHaveTextContent('01012345601');
    expect(input()).toHaveAttribute('placeholder', 'ابحث بالاسم أو رقم الهاتف');
  });
});
