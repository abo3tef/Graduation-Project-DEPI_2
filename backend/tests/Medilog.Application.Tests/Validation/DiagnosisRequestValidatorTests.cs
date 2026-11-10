using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Validation;
using Xunit;

namespace Medilog.Application.Tests.Validation;

public sealed class DiagnosisRequestValidatorTests
{
    private readonly CreateDiagnosisRequestValidator _createValidator = new();
    private readonly UpdateDiagnosisRequestValidator _updateValidator = new();

    [Fact]
    public async Task Valid_create_request_passes()
    {
        var request = new CreateDiagnosisRequest(
            "Acute bronchitis",
            "Dry cough for five days.",
            DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await _createValidator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_name_is_rejected(string name)
    {
        var request = new CreateDiagnosisRequest(name, null, DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await _createValidator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.DiagnosisName));
    }

    [Fact]
    public async Task Name_over_the_limit_is_rejected()
    {
        var request = new CreateDiagnosisRequest(
            new string('d', MedicalRecordText.DiagnosisNameMaxLength + 1),
            null,
            DateOnly.FromDateTime(DateTime.UtcNow));

        var result = await _createValidator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.DiagnosisName));
    }

    [Fact]
    public async Task Future_diagnosis_date_is_rejected()
    {
        var request = new UpdateDiagnosisRequest(
            "Acute bronchitis",
            null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5));

        var result = await _updateValidator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(request.DiagnosisDate));
    }
}
