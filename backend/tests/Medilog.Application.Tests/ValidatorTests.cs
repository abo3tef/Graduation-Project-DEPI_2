using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Appointments;
using Medilog.Application.Features.Appointments.Requests;
using Medilog.Application.Features.Appointments.Validators;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Enums;
using static Medilog.Application.Tests.Support.Scenario;

namespace Medilog.Application.Tests;

public class ValidatorTests
{
    private static BookAppointmentRequest ValidBook() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Day, T("10:00"), T("10:30"), "reason");

    [Fact]
    public void Book_accepts_a_valid_request() =>
        Assert.True(new BookAppointmentRequestValidator().Validate(ValidBook()).IsValid);

    [Fact]
    public void Book_rejects_each_missing_required_value()
    {
        var validator = new BookAppointmentRequestValidator();
        var valid = ValidBook();

        Assert.False(validator.Validate(valid with { PatientId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(valid with { DoctorId = Guid.Empty }).IsValid);
        Assert.False(validator.Validate(valid with { AppointmentDate = null }).IsValid);
        Assert.False(validator.Validate(valid with { StartTime = null }).IsValid);
        Assert.False(validator.Validate(valid with { EndTime = null }).IsValid);
    }

    [Theory]
    [InlineData("10:00", "10:00")]
    [InlineData("10:30", "10:00")]
    public void Book_rejects_an_end_that_is_not_after_the_start(string start, string end)
    {
        var request = ValidBook() with { StartTime = T(start), EndTime = T(end) };

        Assert.False(new BookAppointmentRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Book_limits_the_reason_length()
    {
        var validator = new BookAppointmentRequestValidator();

        Assert.True(validator.Validate(ValidBook() with { Reason = new string('a', AppointmentRules.ReasonMaxLength) }).IsValid);
        Assert.False(validator.Validate(ValidBook() with { Reason = new string('a', AppointmentRules.ReasonMaxLength + 1) }).IsValid);
    }

    [Fact]
    public void Reschedule_requires_date_start_and_a_later_end()
    {
        var validator = new RescheduleAppointmentRequestValidator();

        Assert.True(validator.Validate(new RescheduleAppointmentRequest(Day, T("10:00"), T("10:30"))).IsValid);
        Assert.False(validator.Validate(new RescheduleAppointmentRequest(null, T("10:00"), T("10:30"))).IsValid);
        Assert.False(validator.Validate(new RescheduleAppointmentRequest(Day, null, T("10:30"))).IsValid);
        Assert.False(validator.Validate(new RescheduleAppointmentRequest(Day, T("10:00"), null)).IsValid);
        Assert.False(validator.Validate(new RescheduleAppointmentRequest(Day, T("10:30"), T("10:30"))).IsValid);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("needs to travel", true)]
    public void Cancel_requires_a_non_blank_reason(string? reason, bool valid) =>
        Assert.Equal(valid, new CancelAppointmentRequestValidator().Validate(new CancelAppointmentRequest(reason)).IsValid);

    [Fact]
    public void Cancel_limits_the_reason_to_500_characters_after_trimming()
    {
        var validator = new CancelAppointmentRequestValidator();

        Assert.True(validator.Validate(new CancelAppointmentRequest(new string('a', 500))).IsValid);
        Assert.True(validator.Validate(new CancelAppointmentRequest("  " + new string('a', 500) + "  ")).IsValid);
        Assert.False(validator.Validate(new CancelAppointmentRequest(new string('a', 501))).IsValid);
    }

    [Theory]
    [InlineData(AppointmentStatus.Confirmed, true)]
    [InlineData(AppointmentStatus.Completed, true)]
    [InlineData(AppointmentStatus.NoShow, true)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    [InlineData(null, false)]
    [InlineData((AppointmentStatus)99, false)]
    public void Status_update_accepts_only_defined_non_cancel_statuses(AppointmentStatus? status, bool valid) =>
        Assert.Equal(valid, new UpdateAppointmentStatusRequestValidator().Validate(new UpdateAppointmentStatusRequest(status)).IsValid);

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(1, PagingDefaults.MaxPageSize, true)]
    [InlineData(0, 20, false)]
    [InlineData(-1, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, -5, false)]
    [InlineData(1, PagingDefaults.MaxPageSize + 1, false)]
    public void List_validates_page_number_and_size(int page, int size, bool valid) =>
        Assert.Equal(valid, new ListAppointmentsQueryValidator().Validate(new ListAppointmentsQuery(PageNumber: page, PageSize: size)).IsValid);

    [Fact]
    public void List_rejects_an_inverted_date_range_and_an_undefined_status()
    {
        var validator = new ListAppointmentsQueryValidator();

        Assert.True(validator.Validate(new ListAppointmentsQuery(DateFrom: Day, DateTo: Day)).IsValid);
        Assert.False(validator.Validate(new ListAppointmentsQuery(DateFrom: Day, DateTo: Day.AddDays(-1))).IsValid);
        Assert.False(validator.Validate(new ListAppointmentsQuery(Status: (AppointmentStatus)99)).IsValid);
    }

    [Fact]
    public async Task Service_turns_validation_failures_into_a_request_validation_exception_with_field_errors()
    {
        var s = new Scenario();

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => s.Service.ListAsync(new ListAppointmentsQuery(PageNumber: 0), CancellationToken.None));

        Assert.Contains(nameof(ListAppointmentsQuery.PageNumber), exception.Errors.Keys);
    }

    [Fact]
    public async Task List_returns_paging_metadata_and_total_count()
    {
        var s = new Scenario();
        for (var hour = 8; hour < 13; hour++)
        {
            s.Seed(s.DoctorA, s.PatientA, $"{hour:00}:00", $"{hour:00}:30");
        }

        var page2 = await s.Service.ListAsync(new ListAppointmentsQuery(PageNumber: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(2, page2.PageNumber);
        Assert.Equal([T("10:00"), T("11:00")], page2.Items.Select(i => i.StartTime));
    }
}
