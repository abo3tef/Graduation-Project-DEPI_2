using FluentValidation;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Requests;
using Medilog.Application.Features.MedicalRecords.Validators;
using Medilog.Application.Tests.Support;

namespace Medilog.Application.Tests;

/// <summary>
/// Shape-level request validation (AGENTS §3.1): a bounded, positive page and a
/// non-empty patient id. The role/ownership rules are deliberately NOT here —
/// they are service business rules (see MedicalRecordAuthorizationTests).
/// </summary>
public class MedicalRecordValidatorTests
{
    private readonly GetMedicalProfileRequestValidator _profileValidator = new();
    private readonly GetMedicalHistoryRequestValidator _historyValidator = new();

    [Fact]
    public async Task Profile_requires_a_patient_id()
    {
        var result = await _profileValidator.ValidateAsync(new GetMedicalProfileRequest(Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(GetMedicalProfileRequest.PatientId));
    }

    [Fact]
    public async Task Profile_accepts_a_valid_patient_id()
    {
        var result = await _profileValidator.ValidateAsync(new GetMedicalProfileRequest(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 20)]   // page below the minimum
    [InlineData(-1, 20)]  // negative page
    [InlineData(1, 0)]    // size below the minimum
    [InlineData(1, 101)]  // size above the shared maximum
    public async Task History_rejects_out_of_range_paging(int page, int pageSize)
    {
        var result = await _historyValidator.ValidateAsync(
            new GetMedicalHistoryRequest(Guid.NewGuid(), page, pageSize));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(1, 1)]                       // lower boundary
    [InlineData(1, PagingDefaults.DefaultPageSize)]
    [InlineData(1, PagingDefaults.MaxPageSize)] // upper boundary
    [InlineData(2, 20)]
    public async Task History_accepts_boundary_paging(int page, int pageSize)
    {
        var result = await _historyValidator.ValidateAsync(
            new GetMedicalHistoryRequest(Guid.NewGuid(), page, pageSize));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task History_requires_a_patient_id()
    {
        var result = await _historyValidator.ValidateAsync(new GetMedicalHistoryRequest(Guid.Empty, 1, 20));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, failure => failure.PropertyName == nameof(GetMedicalHistoryRequest.PatientId));
    }
}
