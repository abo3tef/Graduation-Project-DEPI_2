import {describe, expect, it} from 'vitest';
import {isApiError} from '@/lib/api';
import {
  readPayment,
  readPaymentPage,
  readPayableAppointments
} from './payment-readers';

const PAYMENT = {
  id: 'pay-1',
  patientId: 'pat-1',
  patientName: 'Mona Said',
  doctorId: 'doc-1',
  appointmentId: 'apt-1',
  appointmentDate: '2026-11-12',
  amount: 500.5,
  method: 'Cash',
  status: 'Paid',
  paidAt: '2026-11-12T10:00:00+00:00',
  notes: 'First visit'
};

function invalid(action: () => unknown) {
  try {
    action();
  } catch (error) {
    expect(isApiError(error) && error.kind === 'invalid-response').toBe(true);
    return;
  }

  throw new Error('expected an invalid-response error');
}

describe('readPayment', () => {
  it('reads a complete response and maps enums to the lower-case UI values', () => {
    expect(readPayment(PAYMENT)).toEqual({
      ...PAYMENT,
      method: 'cash',
      status: 'paid'
    });
  });

  it('accepts a missing paidAt and notes as null', () => {
    expect(
      readPayment({...PAYMENT, paidAt: undefined, notes: undefined})
    ).toMatchObject({
      paidAt: null,
      notes: null
    });
  });

  it.each([
    'id',
    'patientId',
    'patientName',
    'doctorId',
    'appointmentId',
    'appointmentDate',
    'amount',
    'method',
    'status'
  ])('rejects a missing %s', (field) => {
    invalid(() => readPayment({...PAYMENT, [field]: undefined}));
  });

  it('rejects unknown enums, the wrong casing and wrong types', () => {
    invalid(() => readPayment({...PAYMENT, method: 'cash'}));
    invalid(() => readPayment({...PAYMENT, status: 'paid'}));
    invalid(() => readPayment({...PAYMENT, status: 'Refunded'}));
    invalid(() => readPayment({...PAYMENT, amount: '500'}));
    invalid(() => readPayment({...PAYMENT, amount: Number.POSITIVE_INFINITY}));
    invalid(() => readPayment(null));
    invalid(() => readPayment([PAYMENT]));
  });
});

describe('readPaymentPage', () => {
  it('reads the shared paged envelope', () => {
    const page = readPaymentPage({
      items: [PAYMENT],
      page: 2,
      pageSize: 10,
      totalCount: 11,
      totalPages: 2
    });

    expect(page.items).toHaveLength(1);
    expect(page).toMatchObject({
      page: 2,
      pageSize: 10,
      totalCount: 11,
      totalPages: 2
    });
  });

  it('rejects a malformed envelope or item', () => {
    invalid(() =>
      readPaymentPage({items: 'x', page: 1, pageSize: 10, totalCount: 0, totalPages: 0})
    );
    invalid(() =>
      readPaymentPage({items: [], page: '1', pageSize: 10, totalCount: 0, totalPages: 0})
    );
    invalid(() =>
      readPaymentPage({items: [{id: 'only'}], page: 1, pageSize: 10, totalCount: 1, totalPages: 1})
    );
    invalid(() => readPaymentPage([]));
  });
});

describe('readPayableAppointments', () => {
  it('reads the picker options from the API list', () => {
    expect(
      readPayableAppointments([
        {
          id: 'apt-1',
          patientId: 'pat-1',
          patientName: 'Mona Said',
          doctorId: 'doc-1',
          appointmentDate: '2026-11-12',
          startTime: '10:30:00',
          patientPhone: 'must not be read'
        }
      ])
    ).toEqual([
      {
        id: 'apt-1',
        patientId: 'pat-1',
        patientName: 'Mona Said',
        doctorId: 'doc-1',
        appointmentDate: '2026-11-12',
        startTime: '10:30:00'
      }
    ]);
  });

  it('rejects non-arrays and malformed options', () => {
    invalid(() => readPayableAppointments({}));
    invalid(() => readPayableAppointments([{id: 'apt-1'}]));
    invalid(() => readPayableAppointments(null));
  });
});
