import type {
  Payment,
  PaymentAppointmentOption,
  PaymentMethod,
  PaymentStatus
} from '../types/payment.types';


export const MOCK_APPOINTMENTS: readonly PaymentAppointmentOption[] = [
  {id: 'apt-001', patientName: 'أحمد محمود', appointmentDate: '2026-11-05', startTime: '09:00'},
  {id: 'apt-002', patientName: 'منى السيد', appointmentDate: '2026-11-05', startTime: '09:45'},
  {id: 'apt-003', patientName: 'Omar Hassan', appointmentDate: '2026-11-05', startTime: '10:30'},
  {id: 'apt-004', patientName: 'سارة إبراهيم', appointmentDate: '2026-11-05', startTime: '11:15'},
  {id: 'apt-005', patientName: 'Youssef Adel', appointmentDate: '2026-11-05', startTime: '12:30'},
  {id: 'apt-006', patientName: 'هدى كمال', appointmentDate: '2026-11-05', startTime: '13:30'},
  {id: 'apt-007', patientName: 'Khaled Mostafa', appointmentDate: '2026-11-08', startTime: '08:45'},
  {id: 'apt-008', patientName: 'نور الدين', appointmentDate: '2026-11-08', startTime: '09:30'},
  {id: 'apt-009', patientName: 'Mariam Samir', appointmentDate: '2026-11-08', startTime: '10:00'},
  {id: 'apt-010', patientName: 'عمرو فاروق', appointmentDate: '2026-11-08', startTime: '11:00'},
  {id: 'apt-011', patientName: 'ليلى حسن', appointmentDate: '2026-11-08', startTime: '11:45'},
  {id: 'apt-012', patientName: 'Tarek Nabil', appointmentDate: '2026-11-08', startTime: '12:30'},
  {id: 'apt-013', patientName: 'دعاء عبد الله', appointmentDate: '2026-11-10', startTime: '09:00'},
  {id: 'apt-014', patientName: 'Hana Mahmoud', appointmentDate: '2026-11-10', startTime: '09:45'},
  {id: 'apt-015', patientName: 'كريم صلاح', appointmentDate: '2026-11-10', startTime: '10:30'},
  {id: 'apt-016', patientName: 'رانيا يوسف', appointmentDate: '2026-11-10', startTime: '11:15'},
  {id: 'apt-017', patientName: 'Ali Fathy', appointmentDate: '2026-11-10', startTime: '12:00'},
  {id: 'apt-018', patientName: 'إيمان عطية', appointmentDate: '2026-11-10', startTime: '12:45'},
  {id: 'apt-019', patientName: 'Salma Wael', appointmentDate: '2026-11-11', startTime: '09:00'},
  {id: 'apt-020', patientName: 'محمد سعيد', appointmentDate: '2026-11-11', startTime: '09:45'},
  {id: 'apt-021', patientName: 'فاطمة الزهراء', appointmentDate: '2026-11-11', startTime: '10:45'},
  {id: 'apt-022', patientName: 'Ziad Ayman', appointmentDate: '2026-11-11', startTime: '11:30'},
  {id: 'apt-023', patientName: 'نادية فوزي', appointmentDate: '2026-11-12', startTime: '09:00'},
  {id: 'apt-024', patientName: 'Bassem Tawfik', appointmentDate: '2026-11-12', startTime: '10:00'}
];

type PaymentSeed = readonly [
  appointmentId: string,
  amount: number,
  method: PaymentMethod,
  status: PaymentStatus,
  paidAt: string,
  notes?: string
];

// 22 seeds -> 3 pages with the default page size of 10.
const PAYMENT_SEEDS: readonly PaymentSeed[] = [
  ['apt-001', 500, 'cash', 'paid', '2026-11-05T09:20:00'],
  ['apt-002', 750, 'card', 'paid', '2026-11-05T10:05:00'],
  ['apt-003', 400, 'cash', 'paid', '2026-11-05T11:10:00', 'متابعة'],
  ['apt-004', 1200, 'card', 'partial', '2026-11-05T12:00:00', 'دفعة أولى'],
  ['apt-005', 350, 'cash', 'paid', '2026-11-05T13:15:00'],
  ['apt-006', 600, 'cash', 'pending', '2026-11-05T14:00:00'],
  ['apt-007', 500, 'card', 'paid', '2026-11-08T09:00:00'],
  ['apt-008', 450, 'cash', 'paid', '2026-11-08T09:40:00'],
  ['apt-009', 1000, 'card', 'partial', '2026-11-08T10:30:00', 'Partial payment'],
  ['apt-010', 300, 'cash', 'paid', '2026-11-08T11:20:00'],
  ['apt-011', 750, 'card', 'pending', '2026-11-08T12:10:00'],
  ['apt-012', 500, 'cash', 'paid', '2026-11-08T13:00:00'],
  ['apt-013', 600, 'card', 'paid', '2026-11-10T09:15:00'],
  ['apt-014', 400, 'cash', 'paid', '2026-11-10T10:00:00'],
  ['apt-015', 1200, 'card', 'paid', '2026-11-10T10:45:00'],
  ['apt-016', 500, 'cash', 'pending', '2026-11-10T11:30:00'],
  ['apt-017', 350, 'cash', 'paid', '2026-11-10T12:20:00'],
  ['apt-018', 750, 'card', 'partial', '2026-11-10T13:10:00', 'دفعة أولى'],
  ['apt-019', 500, 'cash', 'paid', '2026-11-11T09:10:00'],
  ['apt-020', 450, 'card', 'paid', '2026-11-11T10:05:00'],
  ['apt-021', 600, 'cash', 'pending', '2026-11-11T11:00:00'],
  ['apt-022', 1000, 'card', 'paid', '2026-11-11T11:50:00']
];

function buildPayment(seed: PaymentSeed, index: number): Payment {
  const [appointmentId, amount, method, status, paidAt, notes] = seed;
  const appointment = MOCK_APPOINTMENTS.find((item) => item.id === appointmentId);

  if (!appointment) {
    throw new Error(`Mock payment references unknown appointment: ${appointmentId}`);
  }

  return {
    id: `pay-${String(index + 1).padStart(3, '0')}`,
    patientName: appointment.patientName,
    appointmentId,
    appointmentDate: appointment.appointmentDate,
    amount,
    method,
    status,
    paidAt,
    ...(notes ? {notes} : {})
  };
}

export const MOCK_PAYMENTS: readonly Payment[] = PAYMENT_SEEDS.map(buildPayment);