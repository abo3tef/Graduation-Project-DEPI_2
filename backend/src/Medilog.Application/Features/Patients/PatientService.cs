using AutoMapper;
using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Patients.Requests;
using Medilog.Application.Features.Patients.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.Patients;

/// <inheritdoc cref="IPatientService"/>
public sealed class PatientService : IPatientService
{
    private const string PatientNotFoundMessage = "The patient was not found.";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IClinicClock _clock;
    private readonly IValidator<CreatePatientRequest> _createValidator;
    private readonly IValidator<UpdatePatientRequest> _updateValidator;
    private readonly IValidator<ListPatientsQuery> _listValidator;
    private readonly IValidator<PatientPickerQuery> _pickerValidator;

    public PatientService(
        IApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IMapper mapper,
        IClinicClock clock,
        IValidator<CreatePatientRequest> createValidator,
        IValidator<UpdatePatientRequest> updateValidator,
        IValidator<ListPatientsQuery> listValidator,
        IValidator<PatientPickerQuery> pickerValidator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _mapper = mapper;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _listValidator = listValidator;
        _pickerValidator = pickerValidator;
    }

    public async Task<PatientResponse> CreateAsync(
        CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_createValidator, request, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);
        RejectMedicalFieldsForSecretary(actor, request.BloodType);

        // The owner comes from the identity. A doctor's body DoctorId is ignored; a secretary must
        // name one of the doctors assigned to them, and anything else looks like a missing doctor.
        Guid ownerId;

        if (actor.IsDoctor)
        {
            ownerId = actor.UserId;
        }
        else
        {
            if (request.DoctorId is not { } requestedDoctorId)
            {
                throw new RequestValidationException(
                    nameof(CreatePatientRequest.DoctorId),
                    "The doctor is required.",
                    "NotNullValidator");
            }

            if (!actor.DoctorIds.Contains(requestedDoctorId))
            {
                throw new NotFoundException("The doctor was not found.", ErrorCodes.DoctorNotFound);
            }

            ownerId = requestedDoctorId;
        }

        var patient = Patient.Create(
            ownerId,
            request.FullName!,
            request.DateOfBirth!.Value,
            request.Gender!.Value,
            request.Phone,
            request.Address,
            request.EmergencyContact,
            actor.IsDoctor ? request.BloodType : null,
            Today());

        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(patient, actor);
    }

    public async Task<PatientResponse> GetAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken);

        var patient = await InScope(actor)
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == patientId, cancellationToken)
            ?? throw PatientNotFound();

        return ToResponse(patient, actor);
    }

    public async Task<PagedResult<PatientResponse>> ListAsync(
        ListPatientsQuery query,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_listValidator, query, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);

        var patients = Filter(InScope(actor, query.DoctorId), query.Search).AsNoTracking();

        if (query.Gender.HasValue)
        {
            var gender = query.Gender.Value;
            patients = patients.Where(patient => patient.Gender == gender);
        }

        var totalCount = await patients.CountAsync(cancellationToken);

        // A long skip avoids int overflow for an absurd page number; a page past the end is empty.
        var skip = (long)(query.PageNumber - 1) * query.PageSize;

        if (skip >= totalCount)
        {
            return new PagedResult<PatientResponse>([], query.PageNumber, query.PageSize, totalCount);
        }

        // Name is not unique, so the id is the stable tie-breaker (AGENTS.md §3.1).
        var page = await patients
            .OrderBy(patient => patient.FullName)
            .ThenBy(patient => patient.Id)
            .Skip((int)skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PatientResponse>(
            page.Select(patient => ToResponse(patient, actor)).ToList(),
            query.PageNumber,
            query.PageSize,
            totalCount);
    }

    public async Task<PatientResponse> UpdateAsync(
        Guid patientId,
        UpdatePatientRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_updateValidator, request, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);
        RejectMedicalFieldsForSecretary(actor, request.BloodType);

        var patient = await InScope(actor)
            .FirstOrDefaultAsync(entity => entity.Id == patientId, cancellationToken)
            ?? throw PatientNotFound();

        patient.UpdateAdministrativeData(
            request.FullName!,
            request.DateOfBirth!.Value,
            request.Gender!.Value,
            request.Phone,
            request.Address,
            request.EmergencyContact,
            Today());

        // Only a doctor owns the medical field; a secretary update never touches the stored value.
        if (actor.IsDoctor)
        {
            patient.ChangeBloodType(request.BloodType);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(patient, actor);
    }

    public async Task DeleteAsync(Guid patientId, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken);

        // Deleting a patient record is the doctor's decision; a secretary only edits administrative data.
        if (!actor.IsDoctor)
        {
            throw new ForbiddenException("Only the owning doctor can delete a patient.");
        }

        var patient = await InScope(actor)
            .FirstOrDefaultAsync(entity => entity.Id == patientId, cancellationToken)
            ?? throw PatientNotFound();

        if (await HasRelatedRecordsAsync(patientId, cancellationToken))
        {
            throw HasRelatedRecords();
        }

        _dbContext.Patients.Remove(patient);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A record linked after the check above is stopped by the restrictive foreign keys
            // (AGENTS.md §4.3); report it as the same conflict instead of a server error.
            throw HasRelatedRecords();
        }
    }

    public async Task<IReadOnlyList<PatientLookupResponse>> LookupAsync(
        PatientPickerQuery query,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_pickerValidator, query, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);

        return await Filter(InScope(actor, query.DoctorId), query.Search)
            .AsNoTracking()
            .OrderBy(patient => patient.FullName)
            .ThenBy(patient => patient.Id)
            .Take(query.Limit)
            .Select(patient => new PatientLookupResponse(patient.Id, patient.FullName, patient.Phone))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Patients the caller may reach: the doctor's own, or those of the secretary's assigned doctors.
    /// A requested doctor can only narrow this set.
    /// </summary>
    private IQueryable<Patient> InScope(Actor actor, Guid? narrowToDoctorId = null)
    {
        IReadOnlyCollection<Guid> doctorIds = narrowToDoctorId is { } doctorId
            ? (actor.DoctorIds.Contains(doctorId) ? [doctorId] : [])
            : actor.DoctorIds;

        return _dbContext.Patients.Where(patient =>
            patient.DoctorId.HasValue && doctorIds.Contains(patient.DoctorId.Value));
    }

    /// <summary>Name or phone contains the term, or the term is the exact patient id.</summary>
    private static IQueryable<Patient> Filter(IQueryable<Patient> patients, string? search)
    {
        var term = search?.Trim();

        if (string.IsNullOrEmpty(term))
        {
            return patients;
        }

        if (Guid.TryParse(term, out var id))
        {
            return patients.Where(patient => patient.Id == id);
        }

        return patients.Where(patient =>
            patient.FullName.Contains(term) || (patient.Phone != null && patient.Phone.Contains(term)));
    }

    private async Task<bool> HasRelatedRecordsAsync(Guid patientId, CancellationToken cancellationToken) =>
        await _dbContext.Appointments.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.MedicalVisits.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.Payments.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.Diagnoses.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.Medications.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.MedicalNotes.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.Allergies.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.ChronicConditions.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.LaboratoryResults.AnyAsync(entity => entity.PatientId == patientId, cancellationToken) ||
        await _dbContext.MedicalDocuments.AnyAsync(entity => entity.PatientId == patientId, cancellationToken);

    private async Task<Actor> RequireActorAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || userId == Guid.Empty ||
            _currentUser.Role is not { } role || !Enum.IsDefined(role))
        {
            throw new ForbiddenException("The current user cannot manage patients.", ErrorCodes.IdentityInvalid);
        }

        switch (role)
        {
            case UserRole.Doctor:
                var doctorActive = await _dbContext.Doctors
                    .AsNoTracking()
                    .AnyAsync(
                        doctor => doctor.Id == userId && doctor.AccountStatus == AccountStatus.Active,
                        cancellationToken);

                return doctorActive
                    ? new Actor(userId, true, [userId])
                    : throw new ForbiddenException("The current user cannot manage patients.", ErrorCodes.IdentityInvalid);

            case UserRole.Secretary:
                var secretary = await _dbContext.Secretaries
                    .AsNoTracking()
                    .Where(entity => entity.Id == userId && entity.AccountStatus == AccountStatus.Active)
                    .Select(entity => new { DoctorIds = entity.Doctors.Select(doctor => doctor.Id).ToList() })
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new ForbiddenException("The current user cannot manage patients.", ErrorCodes.IdentityInvalid);

                return new Actor(userId, false, secretary.DoctorIds);

            default:
                throw new ForbiddenException("The current user cannot manage patients.", ErrorCodes.IdentityInvalid);
        }
    }

    private static void RejectMedicalFieldsForSecretary(Actor actor, BloodType? bloodType)
    {
        if (!actor.IsDoctor && bloodType.HasValue)
        {
            throw new ForbiddenException(
                "A secretary cannot set medical fields.",
                ErrorCodes.PatientMedicalFieldForbidden);
        }
    }

    private PatientResponse ToResponse(Patient patient, Actor actor) =>
        _mapper.Map<PatientResponse>(
            patient,
            options => options.Items[PatientsMappingProfile.IncludeMedicalKey] = actor.IsDoctor);

    private DateOnly Today() => DateOnly.FromDateTime(_clock.LocalNow);

    private static NotFoundException PatientNotFound() =>
        new(PatientNotFoundMessage, ErrorCodes.PatientNotFound);

    private static ConflictException HasRelatedRecords() =>
        new(
            "The patient has appointments, visits, payments or other records and cannot be deleted.",
            ErrorCodes.PatientHasRelatedRecords);

    /// <summary>The authenticated caller and the doctors whose patients they may reach.</summary>
    private sealed record Actor(Guid UserId, bool IsDoctor, IReadOnlyCollection<Guid> DoctorIds);
}
