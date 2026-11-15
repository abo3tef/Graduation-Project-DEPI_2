import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest';
import {isApiError, resetAuthHeadersProviders} from '@/lib/api';
import {
  createPayment,
  isPaymentPartiallyRecordedError,
  listPayableAppointments,
  listPayments,
  updatePaymentStatus
} from './payments.service';

const PENDING_PAYMENT = {
  id: 'pay-1',
  patientId: 'pat-1',
  patientName: 'Mona Said',
  doctorId: 'doc-1',
  appointmentId: 'apt-1',
  appointmentDate: '2026-11-12',
  amount: 250.5,
  method: 'Card',
  status: 'Pending',
  paidAt: null,
  notes: 'First visit'
};

const PAID_PAYMENT = {
  ...PENDING_PAYMENT,
  status: 'Paid',
  paidAt: '2026-11-12T10:00:00+00:00'
};

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {'content-type': 'application/json'}
  });
}

const fetchMock = vi.fn<typeof fetch>();

beforeEach(() => {
  vi.stubEnv('NEXT_PUBLIC_API_BASE_URL', 'http://api.test');
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  resetAuthHeadersProviders();
  fetchMock.mockReset();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
});

describe('listPayments', () => {
  it('sends the filters in the API casing and reads the paged result', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(200, {
        items: [PENDING_PAYMENT],
        page: 2,
        pageSize: 10,
        totalCount: 11,
        totalPages: 2
      })
    );

    const page = await listPayments({
      search: '  mona ',
      status: 'paid',
      method: 'card',
      page: 2,
      pageSize: 10
    });

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'http://api.test/api/payments?search=mona&status=Paid&method=Card&pageNumber=2&pageSize=10'
    );
    expect(page.items[0]).toMatchObject({method: 'card', status: 'pending'});
  });

  it('omits the "all" filters and an empty search', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(200, {items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0})
    );

    await listPayments({search: '   ', status: 'all', method: 'all', page: 1, pageSize: 10});

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'http://api.test/api/payments?pageNumber=1&pageSize=10'
    );
  });
});

describe('listPayableAppointments', () => {
  it('asks for the bounded picker list', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(200, [
        {
          id: 'apt-1',
          patientId: 'pat-1',
          patientName: 'Mona Said',
          doctorId: 'doc-1',
          appointmentDate: '2026-11-12',
          startTime: '10:30:00'
        }
      ])
    );

    const options = await listPayableAppointments();

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'http://api.test/api/payments/payable-appointments?limit=50'
    );
    expect(options).toHaveLength(1);
    expect(options[0]?.startTime).toBe('10:30:00');
  });
});

describe('createPayment', () => {
  it('creates a pending payment with the PascalCase method and trimmed notes', async () => {
    fetchMock.mockResolvedValue(jsonResponse(201, PENDING_PAYMENT));

    const payment = await createPayment({
      appointmentId: 'apt-1',
      amount: 250.5,
      method: 'card',
      notes: '  First visit  '
    });

    const init = fetchMock.mock.calls[0]?.[1];
    expect(fetchMock.mock.calls[0]?.[0]).toBe('http://api.test/api/payments');
    expect(init?.method).toBe('POST');
    expect(init?.body).toBe(
      JSON.stringify({
        appointmentId: 'apt-1',
        amount: 250.5,
        method: 'Card',
        notes: 'First visit'
      })
    );
    expect(payment).toMatchObject({status: 'pending', paidAt: null});
  });

  it('sends null notes for an empty note', async () => {
    fetchMock.mockResolvedValue(jsonResponse(201, PENDING_PAYMENT));

    await createPayment({appointmentId: 'apt-1', amount: 10, method: 'cash', notes: '   '});

    const init = fetchMock.mock.calls[0]?.[1];
    expect(JSON.parse(String(init?.body))).toMatchObject({notes: null});
  });

  it('follows the POST with a status call when recordAsPaid is set', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(201, PENDING_PAYMENT))
      .mockResolvedValueOnce(jsonResponse(200, PAID_PAYMENT));

    const payment = await createPayment({
      appointmentId: 'apt-1',
      amount: 250.5,
      method: 'card',
      recordAsPaid: true
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    const [url, init] = fetchMock.mock.calls[1] ?? [];
    expect(url).toBe('http://api.test/api/payments/pay-1/status');
    expect(init?.method).toBe('PUT');
    expect(init?.body).toBe(JSON.stringify({status: 'Paid'}));
    expect(payment).toMatchObject({status: 'paid', paidAt: PAID_PAYMENT.paidAt});
  });

  it('reports a partial failure when the payment was created but not paid', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(201, PENDING_PAYMENT))
      .mockResolvedValueOnce(jsonResponse(409, {code: 'payment.paid_final'}));

    const error = await createPayment({
      appointmentId: 'apt-1',
      amount: 250.5,
      method: 'card',
      recordAsPaid: true
    }).catch((failure: unknown) => failure);

    expect(isPaymentPartiallyRecordedError(error)).toBe(true);

    if (!isPaymentPartiallyRecordedError(error)) {
      throw new Error('expected a partial recording error');
    }

    expect(error.payment).toMatchObject({id: 'pay-1', status: 'pending'});
    expect(isApiError(error.causeError) && error.causeError.code).toBe('payment.paid_final');
  });
});

describe('updatePaymentStatus', () => {
  it('sends the new status in the API casing and reads the updated payment', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(200, {...PENDING_PAYMENT, status: 'Partial'})
    );

    const payment = await updatePaymentStatus({id: 'pay-1', status: 'partial'});

    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('http://api.test/api/payments/pay-1/status');
    expect(init?.method).toBe('PUT');
    expect(init?.body).toBe(JSON.stringify({status: 'Partial'}));
    expect(payment.status).toBe('partial');
  });
});
