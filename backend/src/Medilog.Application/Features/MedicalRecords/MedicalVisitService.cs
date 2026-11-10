using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Security;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Medilog.Application.Features.MedicalRecords;

/// <summary>
/// Implements the medical-visit use cases on top of the persistence abstraction.
/// </summary>
/// <remarks>
/// <para>
/// The acting doctor is always taken from <see cref="ICurrentUser"/>; no method accepts a doctor
/// identifier, so a caller cannot write into another doctor's records (AGENTS.md §5).
/// </para>
/// <para>
/// Each public method validates its request before any persistence call. State rules (existence,
/// workspace ownership, appointment consistency) are enforced here rather than in the validators
/// (AGENTS.md §3.2).
/// </para>
/// </remarks>
public sealed class MedicalVisitService : IMedicalVisitService
{
    private const string VisitNotFoundMessage = "The medical visit was not found.";
    private const string DiagnosisNotFoundMessage = "The diagnosis was not found.";

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateMedicalVisitRequest> _createVisitValidator;
    private readonly IValidator<UpdateMedicalVisitRequest> _updateVisitValidator;
    private readonly IValidator<CreateDiagnosisRequest> _createDiagnosisValidator;
    private readonly IValidator<UpdateDiagnosisRequest> _updateDiagnosisValidator;

    public MedicalVisitService(
        IApplicationDbContext dbContext,
        ICurrentUser currentUser,
        IMapper mapper,
        IValidator<CreateMedicalVisitRequest> createVisitValidator,
        IValidator<UpdateMedicalVisitRequest> updateVisitValidator,
        IValidator<CreateDiagnosisRequest> createDiagnosisValidator,
        IValidator<UpdateDiagnosisRequest> updateDiagnosisValidator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _mapper = mapper;
        _createVisitValidator = createVisitValidator;
        _updateVisitValidator = updateVisitValidator;
        _createDiagnosisValidator = createDiagnosisValidator;
        _updateDiagnosisValidator = updateDiagnosisValidator;
    }

    public async Task<MedicalVisitResponse> CreateVisitAsync(
        CreateMedicalVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createVisitValidator.ValidateAndThrowAsync(request, cancellationToken);

        var doctorId = RequireActingDoctorId();

        var patient = await _dbContext.Patients
            .FirstOrDefaultAsync(candidate => candidate.Id == request.PatientId, cancellationToken)
            ?? throw new NotFoundException("The patient was not found.");

        if (!MedicalRecordAccessPolicy.CanAccessPatient(patient.DoctorId, doctorId))
        {
            throw new ForbiddenException("The patient is not in the acting doctor's workspace.");
        }

        if (request.AppointmentId is { } appointmentId)
        {
            await EnsureAppointmentCanBeLinkedAsync(appointmentId, patient.Id, doctorId, cancellationToken);
        }

        var visit = _mapper.Map<MedicalVisit>(request);

        visit.DoctorId = doctorId;
        visit.PatientId = patient.Id;

        _dbContext.MedicalVisits.Add(visit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<MedicalVisitResponse>(visit);
    }

    public async Task<MedicalVisitResponse> GetVisitAsync(
        Guid visitId,
        CancellationToken cancellationToken = default)
    {
        var doctorId = RequireActingDoctorId();

        var visit = await _dbContext.MedicalVisits
            .AsNoTracking()
            .Include(candidate => candidate.Diagnoses)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == visitId && candidate.DoctorId == doctorId,
                cancellationToken)
            ?? throw new NotFoundException(VisitNotFoundMessage);

        return _mapper.Map<MedicalVisitResponse>(visit);
    }

    public async Task<MedicalVisitResponse> UpdateVisitAsync(
        Guid visitId,
        UpdateMedicalVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateVisitValidator.ValidateAndThrowAsync(request, cancellationToken);

        var doctorId = RequireActingDoctorId();

        var visit = await _dbContext.MedicalVisits
            .Include(candidate => candidate.Diagnoses)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == visitId && candidate.DoctorId == doctorId,
                cancellationToken)
            ?? throw new NotFoundException(VisitNotFoundMessage);

        _mapper.Map(request, visit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<MedicalVisitResponse>(visit);
    }

    public async Task<DiagnosisResponse> AddDiagnosisAsync(
        Guid visitId,
        CreateDiagnosisRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createDiagnosisValidator.ValidateAndThrowAsync(request, cancellationToken);

        var doctorId = RequireActingDoctorId();

        var visit = await _dbContext.MedicalVisits
            .FirstOrDefaultAsync(
                candidate => candidate.Id == visitId && candidate.DoctorId == doctorId,
                cancellationToken)
            ?? throw new NotFoundException(VisitNotFoundMessage);

        var diagnosis = _mapper.Map<Diagnosis>(request);

        diagnosis.VisitId = visit.Id;
        diagnosis.PatientId = visit.PatientId;

        _dbContext.Diagnoses.Add(diagnosis);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<DiagnosisResponse>(diagnosis);
    }

    public async Task<DiagnosisResponse> UpdateDiagnosisAsync(
        Guid visitId,
        Guid diagnosisId,
        UpdateDiagnosisRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateDiagnosisValidator.ValidateAndThrowAsync(request, cancellationToken);

        var doctorId = RequireActingDoctorId();

        var diagnosis = await _dbContext.Diagnoses
            .Include(candidate => candidate.Visit)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == diagnosisId
                    && candidate.VisitId == visitId
                    && candidate.Visit.DoctorId == doctorId,
                cancellationToken)
            ?? throw new NotFoundException(DiagnosisNotFoundMessage);

        _mapper.Map(request, diagnosis);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return _mapper.Map<DiagnosisResponse>(diagnosis);
    }

    private async Task EnsureAppointmentCanBeLinkedAsync(
        Guid appointmentId,
        Guid patientId,
        Guid doctorId,
        CancellationToken cancellationToken)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(candidate => candidate.Id == appointmentId, cancellationToken)
            ?? throw new NotFoundException("The appointment was not found.");

        if (appointment.PatientId != patientId || appointment.DoctorId != doctorId)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(
                    nameof(CreateMedicalVisitRequest.AppointmentId),
                    "The appointment must belong to the same patient and the acting doctor."),
            });
        }

        var alreadyLinked = await _dbContext.MedicalVisits
            .AnyAsync(candidate => candidate.AppointmentId == appointmentId, cancellationToken);

        if (alreadyLinked)
        {
            throw new ConflictException("The appointment is already linked to a medical visit.");
        }
    }

    /// <summary>
    /// Resolves the authenticated doctor or refuses the operation. Defence in depth: the endpoint
    /// layer also enforces the Doctor role, but the use case must not rely on the caller for this.
    /// </summary>
    private Guid RequireActingDoctorId()
    {
        if (!string.Equals(_currentUser.Role, Roles.Doctor, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Only an authenticated doctor may access medical records.");
        }

        return _currentUser.UserId
            ?? throw new ForbiddenException("The authenticated doctor could not be identified.");
    }
}
