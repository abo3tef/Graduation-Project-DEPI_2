using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Features.Payments.Requests;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Common;
using Medilog.Domain.Enums;
using Xunit;

namespace Medilog.Application.Tests.Payments;

/// <summary>
/// Application rules for the payment use cases: server-derived ownership, doctor
/// scoping (a secretary only reaches assigned doctors), the 409 conflict paths and
/// the payable-appointment lookup. The acting user always comes from the identity,
/// never from the request body (AGENTS.md §5).
/// </summary>
public sealed class PaymentServiceTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Fact]
    public async Task Create_uses_the_authenticated_recorder_and_starts_pending()
    {
        var scenario = new PaymentScenario();

        var response = await scenario.CreateAsync(amount: 250.50m, method: PaymentMethod.Card, notes: " first ");

        Assert.Equal(PaymentStatus.Pending, response.Status);
        Assert.Equal(250.50m, response.Amount);
        Assert.Equal(PaymentMethod.Card, response.Method);
        Assert.Equal("first", response.Notes);
        Assert.Null(response.PaidAt);

        // Ownership comes from the appointment, not from the client.
        Assert.Equal(scenario.AppointmentA.Id, response.AppointmentId);
        Assert.Equal(scenario.PatientA.Id, response.PatientId);
        Assert.Equal("Patient A", response.PatientName);
        Assert.Equal(scenario.DoctorA.Id, response.DoctorId);
        Assert.Equal(PaymentScenario.Day, response.AppointmentDate);

        var stored = Assert.Single(scenario.Store.Payments);
        Assert.Equal(scenario.Secretary.Id, stored.CreatedBy);
    }

    [Fact]
    public async Task A_doctor_can_record_a_payment_for_their_own_appointment()
    {
        var scenario = new PaymentScenario();
        scenario.ActAsDoctor(scenario.DoctorA);

        var response = await scenario.CreateAsync();

        Assert.Equal(scenario.DoctorA.Id, response.DoctorId);
        Assert.Equal(scenario.DoctorA.Id, Assert.Single(scenario.Store.Payments).CreatedBy);
    }

    [Fact]
    public async Task A_doctor_cannot_record_a_payment_for_another_doctors_appointment()
    {
        var scenario = new PaymentScenario();
        scenario.ActAsDoctor(scenario.DoctorA);

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => scenario.CreateAsync(appointmentId: scenario.AppointmentB.Id));

        Assert.Equal(ErrorCodes.PaymentAppointmentNotFound, error.Code);
        Assert.Empty(scenario.Store.Payments);
    }

    [Fact]
    public async Task A_secretary_cannot_record_a_payment_for_an_unassigned_doctor()
    {
        var scenario = new PaymentScenario();

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => scenario.CreateAsync(appointmentId: scenario.AppointmentB.Id));

        Assert.Equal(ErrorCodes.PaymentAppointmentNotFound, error.Code);
    }

    [Fact]
    public async Task An_assigned_secretary_can_record_a_payment()
    {
        var scenario = new PaymentScenario();
        scenario.AssignSecretaryTo(scenario.DoctorB);

        var response = await scenario.CreateAsync(appointmentId: scenario.AppointmentB.Id);

        Assert.Equal(scenario.DoctorB.Id, response.DoctorId);
    }

    [Fact]
    public async Task An_unknown_appointment_is_reported_as_not_found()
    {
        var scenario = new PaymentScenario();

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => scenario.CreateAsync(appointmentId: Guid.NewGuid()));

        Assert.Equal(ErrorCodes.PaymentAppointmentNotFound, error.Code);
    }

    [Fact]
    public async Task A_second_payment_for_the_same_appointment_is_a_conflict()
    {
        var scenario = new PaymentScenario();
        await scenario.CreateAsync();

        var error = await Assert.ThrowsAsync<ConflictException>(() => scenario.CreateAsync());

        Assert.Equal(ErrorCodes.PaymentAlreadyExists, error.Code);
        Assert.Single(scenario.Store.Payments);
    }

    [Fact]
    public async Task List_is_scoped_to_the_secretarys_assigned_doctors()
    {
        var scenario = new PaymentScenario();
        scenario.Seed(scenario.DoctorA, scenario.PatientA, scenario.AppointmentA);
        scenario.Seed(scenario.DoctorB, scenario.PatientB, scenario.AppointmentB);

        var scoped = await scenario.Service.ListAsync(new ListPaymentsQuery(), None);
        Assert.Equal(1, scoped.TotalCount);
        Assert.All(scoped.Items, item => Assert.Equal(scenario.DoctorA.Id, item.DoctorId));

        scenario.AssignSecretaryTo(scenario.DoctorB);
        var widened = await scenario.Service.ListAsync(new ListPaymentsQuery(), None);
        Assert.Equal(2, widened.TotalCount);
    }

    [Fact]
    public async Task A_client_supplied_doctor_id_can_only_narrow_the_visible_scope()
    {
        var scenario = new PaymentScenario();
        scenario.Seed(scenario.DoctorA, scenario.PatientA, scenario.AppointmentA);
        scenario.Seed(scenario.DoctorB, scenario.PatientB, scenario.AppointmentB);
        scenario.ActAsDoctor(scenario.DoctorA);

        var own = await scenario.Service.ListAsync(new ListPaymentsQuery(), None);
        Assert.Equal(1, own.TotalCount);
        Assert.Equal(scenario.DoctorA.Id, Assert.Single(own.Items).DoctorId);

        var other = await scenario.Service.ListAsync(
            new ListPaymentsQuery(DoctorId: scenario.DoctorB.Id),
            None);
        Assert.Equal(0, other.TotalCount);
        Assert.Empty(other.Items);
    }

    [Fact]
    public async Task List_filters_by_status_method_patient_and_search()
    {
        var scenario = new PaymentScenario();
        var secondAppointment = scenario.AddAppointment(
            scenario.DoctorA, scenario.PatientA, PaymentScenario.Day.AddDays(3));
        scenario.Seed(
            scenario.DoctorA, scenario.PatientA, scenario.AppointmentA,
            status: PaymentStatus.Paid, method: PaymentMethod.Card, paidAt: PaymentScenario.Now);
        scenario.Seed(
            scenario.DoctorA, scenario.PatientA, secondAppointment,
            status: PaymentStatus.Pending, method: PaymentMethod.Cash);

        var paid = await scenario.Service.ListAsync(
            new ListPaymentsQuery(Status: PaymentStatus.Paid), None);
        Assert.Equal(1, paid.TotalCount);
        Assert.Equal(PaymentStatus.Paid, Assert.Single(paid.Items).Status);

        var cash = await scenario.Service.ListAsync(
            new ListPaymentsQuery(Method: PaymentMethod.Cash), None);
        Assert.Equal(1, cash.TotalCount);
        Assert.Equal(secondAppointment.Id, Assert.Single(cash.Items).AppointmentId);

        var byPatient = await scenario.Service.ListAsync(
            new ListPaymentsQuery(PatientId: scenario.PatientA.Id), None);
        Assert.Equal(2, byPatient.TotalCount);

        var byName = await scenario.Service.ListAsync(
            new ListPaymentsQuery(Search: "patient a"), None);
        Assert.Equal(2, byName.TotalCount);

        var byAppointmentId = await scenario.Service.ListAsync(
            new ListPaymentsQuery(Search: secondAppointment.Id.ToString()), None);
        Assert.Equal(secondAppointment.Id, Assert.Single(byAppointmentId.Items).AppointmentId);

        var noMatch = await scenario.Service.ListAsync(
            new ListPaymentsQuery(Search: "Patient B"), None);
        Assert.Equal(0, noMatch.TotalCount);
    }

    [Fact]
    public async Task List_filters_by_the_billed_appointment_date()
    {
        var scenario = new PaymentScenario();
        var laterAppointment = scenario.AddAppointment(
            scenario.DoctorA, scenario.PatientA, PaymentScenario.Day.AddDays(10));
        scenario.Seed(scenario.DoctorA, scenario.PatientA, scenario.AppointmentA);
        scenario.Seed(scenario.DoctorA, scenario.PatientA, laterAppointment);

        var inRange = await scenario.Service.ListAsync(
            new ListPaymentsQuery(DateFrom: PaymentScenario.Day, DateTo: PaymentScenario.Day), None);
        Assert.Equal(PaymentScenario.Day, Assert.Single(inRange.Items).AppointmentDate);

        var fromLater = await scenario.Service.ListAsync(
            new ListPaymentsQuery(DateFrom: PaymentScenario.Day.AddDays(5)), None);
        Assert.Equal(laterAppointment.Id, Assert.Single(fromLater.Items).AppointmentId);
    }

    [Fact]
    public async Task List_pages_with_a_stable_total_count_and_order()
    {
        var scenario = new PaymentScenario();
        var appointments = new[]
        {
            scenario.AppointmentA,
            scenario.AddAppointment(scenario.DoctorA, scenario.PatientA, PaymentScenario.Day.AddDays(1)),
            scenario.AddAppointment(scenario.DoctorA, scenario.PatientA, PaymentScenario.Day.AddDays(2)),
        };

        for (var index = 0; index < appointments.Length; index++)
        {
            scenario.Seed(
                scenario.DoctorA, scenario.PatientA, appointments[index],
                amount: 100m + index,
                status: PaymentStatus.Paid,
                paidAt: PaymentScenario.Now.AddHours(index));
        }

        var first = await scenario.Service.ListAsync(
            new ListPaymentsQuery(PageNumber: 1, PageSize: 2), None);
        var second = await scenario.Service.ListAsync(
            new ListPaymentsQuery(PageNumber: 2, PageSize: 2), None);

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal(2, first.TotalPages);
        Assert.Single(second.Items);
        Assert.Equal(3, second.TotalCount);

        // Newest paid first, and no row appears on both pages.
        Assert.All(first.Items, item => Assert.NotNull(item.PaidAt));
        Assert.All(first.Items, item => Assert.True(item.PaidAt >= second.Items[0].PaidAt));
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
    }

    [Fact]
    public async Task Get_hides_a_payment_of_another_doctors_scope()
    {
        var scenario = new PaymentScenario();
        var payment = scenario.Seed(scenario.DoctorB, scenario.PatientB, scenario.AppointmentB);

        var asUnassignedSecretary = await Assert.ThrowsAsync<NotFoundException>(
            () => scenario.Service.GetAsync(payment.Id, None));
        Assert.Equal(ErrorCodes.PaymentNotFound, asUnassignedSecretary.Code);

        scenario.ActAsDoctor(scenario.DoctorA);
        var asOtherDoctor = await Assert.ThrowsAsync<NotFoundException>(
            () => scenario.Service.GetAsync(payment.Id, None));
        Assert.Equal(ErrorCodes.PaymentNotFound, asOtherDoctor.Code);

        scenario.ActAsDoctor(scenario.DoctorB);
        var own = await scenario.Service.GetAsync(payment.Id, None);
        Assert.Equal(payment.Id, own.Id);
        Assert.Equal("Patient B", own.PatientName);
    }

    [Fact]
    public async Task Update_changes_the_details_of_a_payment_that_is_not_paid()
    {
        var scenario = new PaymentScenario();
        var payment = scenario.Seed(
            scenario.DoctorA, scenario.PatientA, scenario.AppointmentA, status: PaymentStatus.Partial);

        var response = await scenario.Service.UpdateAsync(
            payment.Id,
            new UpdatePaymentRequest(999.99m, PaymentMethod.Card, " corrected "),
            None);

        Assert.Equal(999.99m, response.Amount);
        Assert.Equal(PaymentMethod.Card, response.Method);
        Assert.Equal("corrected", response.Notes);
        Assert.Equal(PaymentStatus.Partial, response.Status);
        Assert.Equal(payment.Id, response.Id);
    }

    [Fact]
    public async Task Updating_a_paid_payment_is_a_conflict()
    {
        var scenario = new PaymentScenario();
        var payment = scenario.Seed(
            scenario.DoctorA, scenario.PatientA, scenario.AppointmentA,
            status: PaymentStatus.Paid, paidAt: PaymentScenario.Now);

        var error = await Assert.ThrowsAsync<ConflictException>(() => scenario.Service.UpdateAsync(
            payment.Id,
            new UpdatePaymentRequest(10m, PaymentMethod.Cash, null),
            None));

        Assert.Equal(DomainErrorCodes.PaymentPaidFinal, error.Code);
        Assert.Equal(500m, scenario.Store.Payments.Single().Amount);
    }

    [Fact]
    public async Task Status_moves_pending_to_partial_to_paid_and_stamps_the_paid_time()
    {
        var scenario = new PaymentScenario();
        var payment = scenario.Seed(scenario.DoctorA, scenario.PatientA, scenario.AppointmentA);

        var partial = await scenario.Service.UpdateStatusAsync(
            payment.Id, new UpdatePaymentStatusRequest(PaymentStatus.Partial), None);
        Assert.Equal(PaymentStatus.Partial, partial.Status);
        Assert.Null(partial.PaidAt);

        var paid = await scenario.Service.UpdateStatusAsync(
            payment.Id, new UpdatePaymentStatusRequest(PaymentStatus.Paid), None);
        Assert.Equal(PaymentStatus.Paid, paid.Status);
        Assert.Equal(scenario.Clock.UtcNow, paid.PaidAt);
    }

    [Fact]
    public async Task Re_applying_the_current_status_is_a_conflict()
    {
        var scenario = new PaymentScenario();
        var payment = scenario.Seed(scenario.DoctorA, scenario.PatientA, scenario.AppointmentA);

        var error = await Assert.ThrowsAsync<ConflictException>(() => scenario.Service.UpdateStatusAsync(
            payment.Id, new UpdatePaymentStatusRequest(PaymentStatus.Pending), None));

        Assert.Equal(DomainErrorCodes.PaymentInvalidStatusTransition, error.Code);
        Assert.Equal(PaymentStatus.Pending, scenario.Store.Payments.Single().Status);
    }

    [Fact]
    public async Task Paid_cannot_go_back_to_partial()
    {
        var scenario = new PaymentScenario();
        var payment = scenario.Seed(
            scenario.DoctorA, scenario.PatientA, scenario.AppointmentA,
            status: PaymentStatus.Paid, paidAt: PaymentScenario.Now);

        var error = await Assert.ThrowsAsync<ConflictException>(() => scenario.Service.UpdateStatusAsync(
            payment.Id, new UpdatePaymentStatusRequest(PaymentStatus.Partial), None));

        Assert.Equal(DomainErrorCodes.PaymentInvalidStatusTransition, error.Code);
        Assert.Equal(PaymentStatus.Paid, scenario.Store.Payments.Single().Status);
    }

    [Fact]
    public async Task A_payment_outside_the_scope_cannot_be_updated()
    {
        var scenario = new PaymentScenario();
        var payment = scenario.Seed(scenario.DoctorB, scenario.PatientB, scenario.AppointmentB);

        var update = await Assert.ThrowsAsync<NotFoundException>(() => scenario.Service.UpdateAsync(
            payment.Id, new UpdatePaymentRequest(10m, PaymentMethod.Cash, null), None));
        var status = await Assert.ThrowsAsync<NotFoundException>(() => scenario.Service.UpdateStatusAsync(
            payment.Id, new UpdatePaymentStatusRequest(PaymentStatus.Paid), None));

        Assert.Equal(ErrorCodes.PaymentNotFound, update.Code);
        Assert.Equal(ErrorCodes.PaymentNotFound, status.Code);
    }

    [Fact]
    public async Task Payable_appointments_are_scoped_and_exclude_billed_appointments()
    {
        var scenario = new PaymentScenario();

        // Doctor B's appointment is outside the secretary's scope.
        var initial = await scenario.Service.GetPayableAppointmentsAsync(
            new PayableAppointmentsQuery(), None);
        Assert.Equal(scenario.AppointmentA.Id, Assert.Single(initial).Id);

        await scenario.CreateAsync();
        var afterCreate = await scenario.Service.GetPayableAppointmentsAsync(
            new PayableAppointmentsQuery(), None);
        Assert.Empty(afterCreate);

        scenario.AssignSecretaryTo(scenario.DoctorB);
        var widened = await scenario.Service.GetPayableAppointmentsAsync(
            new PayableAppointmentsQuery(), None);
        Assert.Equal(scenario.AppointmentB.Id, Assert.Single(widened).Id);
    }

    [Fact]
    public async Task Payable_appointments_support_search_and_a_bounded_limit()
    {
        var scenario = new PaymentScenario();
        scenario.AddAppointment(scenario.DoctorA, scenario.PatientA, PaymentScenario.Day.AddDays(1));
        scenario.AddAppointment(scenario.DoctorA, scenario.PatientA, PaymentScenario.Day.AddDays(2));

        var searched = await scenario.Service.GetPayableAppointmentsAsync(
            new PayableAppointmentsQuery(Search: "Patient A"), None);
        Assert.Equal(3, searched.Count);

        var byPatientB = await scenario.Service.GetPayableAppointmentsAsync(
            new PayableAppointmentsQuery(Search: "Patient B"), None);
        Assert.Empty(byPatientB);

        var limited = await scenario.Service.GetPayableAppointmentsAsync(
            new PayableAppointmentsQuery(Limit: 1), None);
        Assert.Single(limited);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, true)]
    [InlineData(AppointmentStatus.Confirmed, true)]
    [InlineData(AppointmentStatus.Completed, true)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.NoShow, false)]
    public async Task Payable_appointments_exclude_cancelled_and_no_show(
        AppointmentStatus status,
        bool expected)
    {
        var scenario = new PaymentScenario();
        scenario.AppointmentA.Status = status;

        var payable = await scenario.Service.GetPayableAppointmentsAsync(
            new PayableAppointmentsQuery(), None);

        Assert.Equal(expected, payable.Any(item => item.Id == scenario.AppointmentA.Id));

        // The unassigned doctor never appears, whatever the appointment status.
        Assert.DoesNotContain(payable, item => item.Id == scenario.AppointmentB.Id);
    }

    [Fact]
    public async Task An_unauthenticated_or_unknown_actor_is_forbidden()
    {
        var scenario = new PaymentScenario();
        scenario.User.UserId = null;

        var anonymous = await Assert.ThrowsAsync<ForbiddenException>(
            () => scenario.Service.ListAsync(new ListPaymentsQuery(), None));
        Assert.Equal(ErrorCodes.IdentityInvalid, anonymous.Code);

        scenario.User.UserId = Guid.NewGuid();
        scenario.User.Role = UserRole.Secretary;

        var unknown = await Assert.ThrowsAsync<ForbiddenException>(
            () => scenario.Service.CreateAsync(
                new CreatePaymentRequest(scenario.AppointmentA.Id, 10m, PaymentMethod.Cash, null),
                None));
        Assert.Equal(ErrorCodes.IdentityInvalid, unknown.Code);
    }

    [Fact]
    public async Task An_inactive_doctor_is_forbidden()
    {
        var scenario = new PaymentScenario();
        scenario.DoctorA.AccountStatus = AccountStatus.Inactive;
        scenario.ActAsDoctor(scenario.DoctorA);

        var error = await Assert.ThrowsAsync<ForbiddenException>(
            () => scenario.Service.GetAsync(Guid.NewGuid(), None));

        Assert.Equal(ErrorCodes.IdentityInvalid, error.Code);
    }
}
