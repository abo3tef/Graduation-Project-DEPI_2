using System.Globalization;
using FluentValidation;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.MedicalRecords.Contracts;

namespace Medilog.Application.Features.MedicalRecords.Validation;

public sealed class ListVitalSignsQueryValidator : PagedQueryValidator<ListVitalSignsQuery>
{
}

public sealed class SaveVitalSignsRequestValidator : AbstractValidator<SaveVitalSignsRequest>
{
    public SaveVitalSignsRequestValidator()
    {
        RuleFor(request => request).Must(request =>
                request.BloodPressure is not null || request.HeartRate is not null
                || request.Temperature is not null || request.RespiratoryRate is not null
                || request.Weight is not null || request.Height is not null || request.SpO2 is not null)
            .WithMessage("At least one measurement is required.");
        RuleFor(request => request.BloodPressure)
            .MaximumLength(VitalSignsLimits.BloodPressureMaxLength)
            .Must(IsValidBloodPressure)
            .WithMessage("Blood pressure must be systolic/diastolic within the allowed ranges, with systolic greater than diastolic.");
        RuleFor(request => request.HeartRate)
            .InclusiveBetween(VitalSignsLimits.HeartRateMin, VitalSignsLimits.HeartRateMax);
        RuleFor(request => request.Temperature)
            .InclusiveBetween(VitalSignsLimits.TemperatureMin, VitalSignsLimits.TemperatureMax);
        RuleFor(request => request.RespiratoryRate)
            .InclusiveBetween(VitalSignsLimits.RespiratoryRateMin, VitalSignsLimits.RespiratoryRateMax);
        RuleFor(request => request.SpO2)
            .InclusiveBetween(VitalSignsLimits.SpO2Min, VitalSignsLimits.SpO2Max);
        RuleFor(request => request.Weight)
            .InclusiveBetween(VitalSignsLimits.WeightMin, VitalSignsLimits.WeightMax);
        RuleFor(request => request.Height)
            .InclusiveBetween(VitalSignsLimits.HeightMin, VitalSignsLimits.HeightMax);
    }

    private static bool IsValidBloodPressure(string? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value.Length > VitalSignsLimits.BloodPressureMaxLength)
        {
            return false;
        }

        var parts = value.Split('/');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var systolic)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var diastolic)
            && systolic >= VitalSignsLimits.SystolicMin && systolic <= VitalSignsLimits.SystolicMax
            && diastolic >= VitalSignsLimits.DiastolicMin && diastolic <= VitalSignsLimits.DiastolicMax
            && systolic > diastolic;
    }
}
