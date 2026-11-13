using FluentValidation;
using Medilog.Application.Common;
using Medilog.Application.Common.Abstractions;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Common.Validation;
using Medilog.Application.Features.Appointments.Lookups;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Features.Appointments.Responses;
using Medilog.Application.Interfaces;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Medilog.Application.Features.Appointments;

/// <inheritdoc cref="IAppointmentService"/>
public sealed class AppointmentService : IAppointmentService
{
    private readonly IAppointmentStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly IClinicClock _clock;
    private readonly IValidator<BookAppointmentRequest> _bookValidator;
    private readonly IValidator<RescheduleAppointmentRequest> _rescheduleValidator;
    private readonly IValidator<CancelAppointmentRequest> _cancelValidator;
    private readonly IValidator<UpdateAppointmentStatusRequest> _statusValidator;
    private readonly IValidator<ListAppointmentsQuery> _listValidator;
    private readonly IValidator<PatientLookupQuery> _patientLookupValidator;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(
        IAppointmentStore store,
        ICurrentUser currentUser,
        IClinicClock clock,
        IValidator<BookAppointmentRequest> bookValidator,
        IValidator<RescheduleAppointmentRequest> rescheduleValidator,
        IValidator<CancelAppointmentRequest> cancelValidator,
        IValidator<UpdateAppointmentStatusRequest> statusValidator,
        IValidator<ListAppointmentsQuery> listValidator,
        IValidator<PatientLookupQuery> patientLookupValidator,
        ILogger<AppointmentService> logger)
    {
        _store = store;
        _currentUser = currentUser;
        _clock = clock;
        _bookValidator = bookValidator;
        _rescheduleValidator = rescheduleValidator;
        _cancelValidator = cancelValidator;
        _statusValidator = statusValidator;
        _listValidator = listValidator;
        _patientLookupValidator = patientLookupValidator;
        _logger = logger;
    }

    public async Task<AppointmentResponse> BookAsync(
        BookAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_bookValidator, request, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);

        // Doctors book for themselves; secretaries only for assigned doctors.
        if (!await CanAccessDoctorAsync(request.DoctorId, cancellationToken))
        {
            throw new NotFoundException("The doctor was not found.", ErrorCodes.DoctorNotFound);
        }

        var doctor = await _store.GetDoctorAsync(request.DoctorId, cancellationToken)
            ?? throw new NotFoundException("The doctor was not found.", ErrorCodes.DoctorNotFound);

        if (!doctor.IsActive)
        {
            throw new ConflictException("The doctor is not available for booking.", ErrorCodes.DoctorUnavailable);
        }

        var patient = await _store.GetPatientAsync(request.PatientId, cancellationToken);

        // A patient belongs to the doctor's workspace (BRULE-03); anything else is
        // reported as not found so another doctor's patients are not revealed.
        if (patient is null || patient.DoctorId != doctor.Id)
        {
            throw new NotFoundException("The patient was not found.", ErrorCodes.PatientNotFound);
        }

        var date = request.AppointmentDate!.Value;
        var start = ToWholeSeconds(request.StartTime!.Value);
        var end = ToWholeSeconds(request.EndTime!.Value);
        EnsureLocalTimesExist(date, start, end);

        // Domain rules: end required and later than start, not in the past.
        var appointment = Appointment.Create(
            patient.Id,
            doctor.Id,
            actor.Id,
            date,
            start,
            end,
            NormalizeText(request.Reason),
            _clock.LocalNow);

        await using var transaction = await _store.BeginDoctorTransactionAsync(doctor.Id, cancellationToken);

        await EnsureNoConflictAsync(doctor.Id, date, start, end, excludeAppointmentId: null, cancellationToken);

        await _store.AddAsync(appointment, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetDetailsOrThrowAsync(appointment.Id, cancellationToken);
    }

    public async Task<AppointmentResponse> RescheduleAsync(
        Guid appointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_rescheduleValidator, request, cancellationToken);

        var date = request.AppointmentDate!.Value;
        var start = ToWholeSeconds(request.StartTime!.Value);
        var end = ToWholeSeconds(request.EndTime!.Value);
        EnsureLocalTimesExist(date, start, end);

        return await ModifyAsync(
            appointmentId,
            async (appointment, token) =>
            {
                appointment.Reschedule(date, start, end, _clock.LocalNow);

                // The appointment's own previous slot is excluded from the check.
                await EnsureNoConflictAsync(appointment.DoctorId, date, start, end, appointment.Id, token);
            },
            cancellationToken);
    }

    public async Task<AppointmentResponse> CancelAsync(
        Guid appointmentId,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_cancelValidator, request, cancellationToken);

        return await ModifyAsync(
            appointmentId,
            (appointment, _) =>
            {
                appointment.Cancel(request.CancellationReason);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public async Task<AppointmentResponse> UpdateStatusAsync(
        Guid appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_statusValidator, request, cancellationToken);

        var newStatus = request.Status!.Value;

        return await ModifyAsync(
            appointmentId,
            (appointment, _) =>
            {
                // Completed is the doctor's decision; Confirmed and No-Show may also
                // be set by a secretary assigned to the doctor.
                if (newStatus == AppointmentStatus.Completed && _currentUser.Role != UserRole.Doctor)
                {
                    throw new ForbiddenException(
                        "Only the doctor can mark an appointment as completed.",
                        ErrorCodes.CompleteRequiresDoctor);
                }

                appointment.UpdateStatus(newStatus);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public async Task<AppointmentResponse> GetAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        await RequireActorAsync(cancellationToken);

        var details = await _store.GetDetailsAsync(appointmentId, cancellationToken);

        if (details is null || !await CanAccessDoctorAsync(details.DoctorId, cancellationToken))
        {
            throw new NotFoundException("The appointment was not found.", ErrorCodes.AppointmentNotFound);
        }

        return details;
    }

    public async Task<PagedResult<AppointmentResponse>> ListAsync(
        ListAppointmentsQuery query,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_listValidator, query, cancellationToken);
        var actor = await RequireActorAsync(cancellationToken);

        // The visible doctors always come from the server-side identity. A client
        // supplied doctor filter can only narrow the set, never widen it.
        IReadOnlyCollection<Guid> visibleDoctorIds = _currentUser.Role == UserRole.Doctor
            ? [actor.Id]
            : await _store.GetAssignedDoctorIdsAsync(actor.Id, cancellationToken);

        if (query.DoctorId.HasValue)
        {
            visibleDoctorIds = visibleDoctorIds.Contains(query.DoctorId.Value)
                ? [query.DoctorId.Value]
                : [];
        }

        if (visibleDoctorIds.Count == 0)
        {
            return new PagedResult<AppointmentResponse>([], query.PageNumber, query.PageSize, 0);
        }

        var criteria = new AppointmentListCriteria(
            visibleDoctorIds,
            query.PatientId,
            query.DateFrom,
            query.DateTo,
            query.Status,
            query.PageNumber,
            query.PageSize);

        return await _store.ListAsync(criteria, cancellationToken);
    }

    public async Task<IReadOnlyList<DoctorLookupResponse>> GetDoctorLookupsAsync(CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken);

        IReadOnlyCollection<Guid> doctorIds = _currentUser.Role == UserRole.Doctor
            ? [actor.Id]
            : await _store.GetAssignedDoctorIdsAsync(actor.Id, cancellationToken);

        return doctorIds.Count == 0
            ? []
            : await _store.GetActiveDoctorLookupsAsync(doctorIds, cancellationToken);
    }

    public async Task<IReadOnlyList<PatientLookupResponse>> SearchPatientsAsync(
        PatientLookupQuery query,
        CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(_patientLookupValidator, query, cancellationToken);
        await RequireActorAsync(cancellationToken);

        // Patients are only searched inside a doctor the caller may act for;
        // anything else looks like a missing doctor.
        if (!await CanAccessDoctorAsync(query.DoctorId, cancellationToken))
        {
            throw new NotFoundException("The doctor was not found.", ErrorCodes.DoctorNotFound);
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        return await _store.SearchPatientsAsync(query.DoctorId, search, query.Limit, cancellationToken);
    }

    /// <summary>
    /// Runs a state change under the per-doctor lock: scope check on a non-tracked
    /// read, lock, fresh tracked read inside the lock, change, save, commit.
    /// </summary>
    private async Task<AppointmentResponse> ModifyAsync(
        Guid appointmentId,
        Func<Appointment, CancellationToken, Task> modify,
        CancellationToken cancellationToken)
    {
        await RequireActorAsync(cancellationToken);

        var doctorId = await _store.GetAppointmentDoctorIdAsync(appointmentId, cancellationToken);

        if (doctorId is null || !await CanAccessDoctorAsync(doctorId.Value, cancellationToken))
        {
            throw new NotFoundException("The appointment was not found.", ErrorCodes.AppointmentNotFound);
        }

        await using (var transaction = await _store.BeginDoctorTransactionAsync(doctorId.Value, cancellationToken))
        {
            // Read inside the lock so the decision uses the latest committed state.
            var appointment = await _store.GetAppointmentAsync(appointmentId, cancellationToken)
                ?? throw new NotFoundException("The appointment was not found.", ErrorCodes.AppointmentNotFound);

            await modify(appointment, cancellationToken);

            await _store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await GetDetailsOrThrowAsync(appointmentId, cancellationToken);
    }

    private async Task<ActorInfo> RequireActorAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || userId == Guid.Empty ||
            _currentUser.Role is not { } role || !Enum.IsDefined(role))
        {
            throw new ForbiddenException("The current user cannot manage appointments.", ErrorCodes.IdentityInvalid);
        }

        var actor = await _store.GetActorAsync(role, userId, cancellationToken);

        if (actor is null || !actor.IsActive)
        {
            throw new ForbiddenException("The current user cannot manage appointments.", ErrorCodes.IdentityInvalid);
        }

        return actor;
    }

    private async Task<bool> CanAccessDoctorAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        return _currentUser.Role == UserRole.Doctor
            ? doctorId == _currentUser.UserId
            : await _store.IsDoctorAssignedAsync(_currentUser.UserId!.Value, doctorId, cancellationToken);
    }

    /// <summary>
    /// Throws when the doctor already has an appointment, still holding its slot,
    /// that overlaps the interval. The rule itself lives in the domain
    /// (<c>newStart &lt; existingEnd &amp;&amp; newEnd &gt; existingStart</c>).
    /// </summary>
    private async Task EnsureNoConflictAsync(
        Guid doctorId,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        Guid? excludeAppointmentId,
        CancellationToken cancellationToken)
    {
        var existing = await _store.GetSlotHoldingAppointmentsAsync(
            doctorId,
            date,
            excludeAppointmentId,
            cancellationToken);

        foreach (var appointment in existing)
        {
            bool overlaps;

            if (appointment.EndTime is null)
            {
                // Legacy row without an end time: treated as a point at its start.
                _logger.LogWarning(
                    "Appointment {AppointmentId} has no end time; treating it as a point at its start time.",
                    appointment.Id);

                overlaps = start <= appointment.StartTime && appointment.StartTime < end;
            }
            else
            {
                overlaps = appointment.OverlapsInterval(date, start, end);
            }

            if (overlaps)
            {
                throw new ConflictException(
                    "The doctor already has an appointment that overlaps the requested time.",
                    ErrorCodes.AppointmentOverlap);
            }
        }
    }

    private void EnsureLocalTimesExist(DateOnly date, TimeOnly start, TimeOnly end)
    {
        if (_clock.IsNonExistentLocalTime(date.ToDateTime(start)))
        {
            throw new RequestValidationException(
                nameof(BookAppointmentRequest.StartTime),
                "The start time does not exist in the clinic time zone on that date.",
                ErrorCodes.TimeNotInTimeZone);
        }

        if (_clock.IsNonExistentLocalTime(date.ToDateTime(end)))
        {
            throw new RequestValidationException(
                nameof(BookAppointmentRequest.EndTime),
                "The end time does not exist in the clinic time zone on that date.",
                ErrorCodes.TimeNotInTimeZone);
        }
    }

    private async Task<AppointmentResponse> GetDetailsOrThrowAsync(Guid appointmentId, CancellationToken cancellationToken)
    {
        return await _store.GetDetailsAsync(appointmentId, cancellationToken)
            ?? throw new NotFoundException("The appointment was not found.", ErrorCodes.AppointmentNotFound);
    }

    /// <summary>The database column is <c>time(0)</c>, so times are whole seconds.</summary>
    private static TimeOnly ToWholeSeconds(TimeOnly time) => new(time.Hour, time.Minute, time.Second);

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
