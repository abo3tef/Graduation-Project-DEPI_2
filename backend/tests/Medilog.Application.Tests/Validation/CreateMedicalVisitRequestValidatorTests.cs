using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Validation;
using Xunit;

namespace Medilog.Application.Tests.Validation;

public sealed class CreateMedicalVisitRequestValidatorTests
{
    private readonly CreateMedicalVisitRequestValidator _validator = new();

    [Fact]
    public async Task Valid_request_passes()
    {
        var request = new CreateMedicalVisitRequest(
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            "Headache",
            null,
            null);

        var result = await _validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Empty_patient_id_is_rejected()
    {
        var request = new CreateMedicalVisitRequest(
            Guid.Empty,
            null,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            null,
            null,
            null);

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.PatientId));
    }

    [Fact]
    public async Task Empty_appointment_id_is_rejected_when_supplied()
    {
        var request = new CreateMedicalVisitRequest(
            Guid.NewGuid(),
            Guid.Empty,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            null,
            null,
            null);

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.AppointmentId));
    }

    [Fact]
    public async Task Visit_date_in_the_future_is_rejected()
    {
        var request = new CreateMedicalVisitRequest(
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow.AddDays(2),
            null,
            null,
            null);

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.VisitDate));
    }

    [Fact]
    public async Task Text_fields_over_their_limit_are_rejected()
    {
        var request = new CreateMedicalVisitRequest(
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            new string('a', MedicalRecordText.ChiefComplaintMaxLength + 1),
            new string('b', MedicalRecordText.SymptomsMaxLength + 1),
            new string('c', MedicalRecordText.ClinicalNotesMaxLength + 1));

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.ChiefComplaint));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.Symptoms));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.ClinicalNotes));
    }

    [Fact]
    public void Whitespace_only_text_is_normalized_to_null()
    {
        Assert.Null(MedicalRecordText.Normalize("   "));
        Assert.Null(MedicalRecordText.Normalize(string.Empty));
        Assert.Null(MedicalRecordText.Normalize(null));
        Assert.Equal("text", MedicalRecordText.Normalize("  text  "));
    }
}
