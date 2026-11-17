using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Patients.Requests;
using Medilog.Application.Features.Patients.Responses;

namespace Medilog.Application.Features.Patients;

/// <summary>
/// Patient management use cases (F8-C1). The acting user and role always come from the
/// authenticated identity: a doctor reaches only their own patients, a secretary only the patients
/// of the doctors assigned to them (<c>doctor_secretaries</c>) and only administrative fields (Q-18).
/// A patient outside the caller's scope behaves exactly like a missing one (404).
/// </summary>
public interface IPatientService
{
    Task<PatientResponse> CreateAsync(CreatePatientRequest request, CancellationToken cancellationToken);

    Task<PatientResponse> GetAsync(Guid patientId, CancellationToken cancellationToken);

    Task<PagedResult<PatientResponse>> ListAsync(ListPatientsQuery query, CancellationToken cancellationToken);

    Task<PatientResponse> UpdateAsync(
        Guid patientId,
        UpdatePatientRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes a patient that has no related records (Q-17). Doctor-only; a patient with any
    /// appointment, visit, payment or other record is rejected with 409 and nothing is cascaded.
    /// </summary>
    Task DeleteAsync(Guid patientId, CancellationToken cancellationToken);

    /// <summary>Small bounded search (id, name, phone only) that powers the patient picker.</summary>
    Task<IReadOnlyList<PatientLookupResponse>> LookupAsync(
        PatientPickerQuery query,
        CancellationToken cancellationToken);
}
