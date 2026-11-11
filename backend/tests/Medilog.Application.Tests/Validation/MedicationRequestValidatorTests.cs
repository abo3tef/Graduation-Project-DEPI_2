using FluentValidation.TestHelper;
using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Validation;
using Xunit;

namespace Medilog.Application.Tests.Validation;

/// <summary>
/// Shape rules for medication requests (decision D-26). The limits mirror the persisted column
/// lengths, so an over-long value cannot reach MySQL.
/// </summary>
public sealed class MedicationRequestValidatorTests
{
    private readonly CreateMedicationRequestValidator _createValidator = new();
    private readonly UpdateMedicationRequestValidator _updateValidator = new();

    [Fact]
    public void Valid_create_request_passes()
    {
        var request = CreateRequest();

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_medication_name(string medicationName)
    {
        var request = CreateRequest() with { MedicationName = medicationName };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.MedicationName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_dosage(string dosage)
    {
        var request = CreateRequest() with { Dosage = dosage };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Dosage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_frequency(string frequency)
    {
        var request = CreateRequest() with { Frequency = frequency };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.Frequency);
    }

    [Fact]
    public void Create_rejects_values_above_the_persisted_column_lengths()
    {
        var request = CreateRequest() with
        {
            MedicationName = new string('a', MedicalRecordText.MedicationNameMaxLength + 1),
            Dosage = new string('b', MedicalRecordText.MedicationDosageMaxLength + 1),
            Frequency = new string('c', MedicalRecordText.MedicationFrequencyMaxLength + 1),
            Duration = new string('d', MedicalRecordText.MedicationDurationMaxLength + 1),
            Instructions = new string('e', MedicalRecordText.MedicationInstructionsMaxLength + 1),
        };

        var result = _createValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.MedicationName);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Dosage);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Frequency);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Duration);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Instructions);
    }

    [Fact]
    public void Optional_fields_may_be_null()
    {
        var request = CreateRequest() with { Duration = null, Instructions = null };

        var result = _createValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Valid_update_request_passes()
    {
        var request = UpdateRequest();

        var result = _updateValidator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Update_applies_the_same_required_rules()
    {
        var request = UpdateRequest() with
        {
            MedicationName = "  ",
            Dosage = string.Empty,
            Frequency = "  ",
        };

        var result = _updateValidator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(candidate => candidate.MedicationName);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Dosage);
        result.ShouldHaveValidationErrorFor(candidate => candidate.Frequency);
    }

    private static CreateMedicationRequest CreateRequest() => new(
        "Amoxicillin",
        "500 mg",
        "Every 8 hours",
        "7 days",
        "Take after food");

    private static UpdateMedicationRequest UpdateRequest() => new(
        "Azithromycin",
        "250 mg",
        "Once daily",
        "5 days",
        null);
}
