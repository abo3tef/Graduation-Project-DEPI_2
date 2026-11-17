export type DashboardRole = "secretary" | "doctor";

export type DashboardAppointment = {
  id: number;
  time: string;
  patient: string;
  doctor: string;
  type: string;
  status: "confirmed" | "waiting" | "completed" | "cancelled";
};

export const secretaryAppointments: DashboardAppointment[] = [
  {
    id: 1,
    time: "09:00",
    patient: "مريم أحمد",
    doctor: "د. أحمد حسن",
    type: "كشف باطنة",
    status: "confirmed",
  },
  {
    id: 2,
    time: "10:30",
    patient: "يوسف علي",
    doctor: "د. سارة خالد",
    type: "متابعة",
    status: "waiting",
  },
  {
    id: 3,
    time: "12:00",
    patient: "نور محمد",
    doctor: "د. أحمد حسن",
    type: "استشارة",
    status: "confirmed",
  },
  {
    id: 4,
    time: "14:30",
    patient: "عمر محمود",
    doctor: "د. سارة خالد",
    type: "كشف باطنة",
    status: "completed",
  },
  {
    id: 5,
    time: "16:00",
    patient: "سلمى حسن",
    doctor: "د. أحمد حسن",
    type: "متابعة",
    status: "waiting",
  },
  {
    id: 6,
    time: "17:30",
    patient: "كريم سمير",
    doctor: "د. سارة خالد",
    type: "استشارة",
    status: "confirmed",
  },
  {
    id: 7,
    time: "18:00",
    patient: "ليلى سامح",
    doctor: "د. أحمد حسن",
    type: "كشف جديد",
    status: "completed",
  },
  {
    id: 8,
    time: "19:00",
    patient: "حسن محمود",
    doctor: "د. سارة خالد",
    type: "متابعة",
    status: "confirmed",
  },
];

export const doctorAppointments: DashboardAppointment[] = [
  {
    id: 11,
    time: "09:00",
    patient: "مريم أحمد",
    doctor: "د. أحمد حسن",
    type: "كشف باطنة",
    status: "completed",
  },
  {
    id: 12,
    time: "10:30",
    patient: "نور محمد",
    doctor: "د. أحمد حسن",
    type: "متابعة",
    status: "waiting",
  },
  {
    id: 13,
    time: "12:00",
    patient: "عمر محمود",
    doctor: "د. أحمد حسن",
    type: "استشارة",
    status: "confirmed",
  },
  {
    id: 14,
    time: "15:00",
    patient: "سلمى حسن",
    doctor: "د. أحمد حسن",
    type: "كشف جديد",
    status: "confirmed",
  },
  {
    id: 15,
    time: "16:30",
    patient: "كريم سمير",
    doctor: "د. أحمد حسن",
    type: "متابعة",
    status: "waiting",
  },
  {
    id: 16,
    time: "18:00",
    patient: "ليلى سامح",
    doctor: "د. أحمد حسن",
    type: "استشارة",
    status: "confirmed",
  },
  {
    id: 17,
    time: "19:00",
    patient: "حسن محمود",
    doctor: "د. أحمد حسن",
    type: "كشف جديد",
    status: "completed",
  },
  {
    id: 18,
    time: "20:00",
    patient: "هدى عادل",
    doctor: "د. أحمد حسن",
    type: "متابعة",
    status: "confirmed",
  },
];

export const weeklyVisits = [38, 52, 44, 68, 57, 76, 63];
