/** Wire types for the F11 account-administration API (`/api/admin`). */

export type AdminRole = 'Admin' | 'Doctor' | 'Secretary';
export type AdminStatus = 'Active' | 'Inactive';
export type AdminRoleFilter = 'all' | AdminRole;
export type AdminStatusFilter = 'all' | AdminStatus;

export type AdminUser = {
  readonly id: string;
  readonly role: AdminRole;
  readonly fullName: string;
  readonly email: string;
  readonly phone: string | null;
  readonly specialization: string | null;
  readonly status: AdminStatus;
  readonly emailVerified: boolean;
  readonly createdAtUtc: string;
};

export type AdminDoctor = {
  readonly id: string;
  readonly fullName: string;
  readonly email: string;
};

export type SecretaryDoctors = {
  readonly secretaryId: string;
  readonly doctors: readonly AdminDoctor[];
};

export type AdminUserFilters = {
  readonly search: string;
  readonly role: AdminRoleFilter;
  readonly status: AdminStatusFilter;
  readonly pageNumber: number;
  readonly pageSize: number;
};

export type SetAdminStatusVariables = {
  readonly id: string;
  readonly status: AdminStatus;
};

export type ChangeAdminRoleVariables = {
  readonly id: string;
  readonly role: Extract<AdminRole, 'Doctor' | 'Secretary'>;
};

export type ReplaceAdminDoctorsVariables = {
  readonly secretaryId: string;
  readonly doctorIds: readonly string[];
};

/** Backend `AdminRules.SearchMaxLength`; keep the UI within the approved contract. */
export const ADMIN_SEARCH_MAX_LENGTH = 100;
/** Backend `AdminRules.MaxDoctorAssignments`. */
export const MAX_DOCTOR_ASSIGNMENTS = 100;
