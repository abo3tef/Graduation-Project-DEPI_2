using Medilog.Application.Features.MedicalRecords.Contracts;
using Medilog.Application.Features.MedicalRecords.Validation;

namespace Medilog.Application.Tests.Validation;

public sealed class VitalSignsRequestValidatorTests
{
    private static readonly SaveVitalSignsRequest Empty = new(null, null, null, null, null, null, null);

    // Literal expectations are Anton's temporary Q-19 decision, pending Tech Lead approval.
    [Theory]
    [InlineData("HeartRate", "20", "300", "19", "301")]
    [InlineData("Temperature", "30.0", "45.0", "29.99", "45.01")]
    [InlineData("RespiratoryRate", "4", "80", "3", "81")]
    [InlineData("SpO2", "50", "100", "49", "101")]
    [InlineData("Weight", "0.3", "500", "0.29", "500.01")]
    [InlineData("Height", "20", "260", "19.99", "260.01")]
    public void Numeric_boundaries_and_optional_fields_follow_temporary_decision(
        string field, string min, string max, string below, string above)
    {
        // Arrange
        var validator = new SaveVitalSignsRequestValidator();
        SaveVitalSignsRequest Request(string value)
        {
            var number = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
            return field switch
            {
                "HeartRate" => Empty with { HeartRate = (int)number },
                "Temperature" => Empty with { Temperature = number },
                "RespiratoryRate" => Empty with { RespiratoryRate = (int)number },
                "SpO2" => Empty with { SpO2 = (int)number },
                "Weight" => Empty with { Weight = number },
                "Height" => Empty with { Height = number },
                _ => throw new ArgumentOutOfRangeException(nameof(field)),
            };
        }

        // Act / Assert: only one field is populated in each valid request.
        Assert.True(validator.Validate(Request(min)).IsValid);
        Assert.True(validator.Validate(Request(max)).IsValid);
        Assert.Contains(validator.Validate(Request(below)).Errors, error => error.PropertyName == field);
        Assert.Contains(validator.Validate(Request(above)).Errors, error => error.PropertyName == field);
    }

    [Theory]
    [InlineData("50/20", true)]
    [InlineData("300/200", true)]
    [InlineData("120/80", true)]
    [InlineData("49/20", false)]
    [InlineData("301/80", false)]
    [InlineData("120/19", false)]
    [InlineData("250/201", false)]
    [InlineData("80/80", false)]
    [InlineData("80/120", false)]
    [InlineData("", false)]
    [InlineData("120", false)]
    [InlineData("120-80", false)]
    [InlineData("120/80/70", false)]
    [InlineData("120 /80", false)]
    [InlineData("+120/80", false)]
    [InlineData("120.5/80", false)]
    [InlineData("abc/80", false)]
    [InlineData("00000000000000120/80", true)]
    [InlineData("000000000000000120/80", false)]
    public void Blood_pressure_format_ranges_order_and_length(string value, bool expected)
    {
        var request = Empty with { BloodPressure = value };
        var result = new SaveVitalSignsRequestValidator().Validate(request);
        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void All_null_is_rejected_and_all_seven_valid_fields_are_accepted()
    {
        var validator = new SaveVitalSignsRequestValidator();
        Assert.False(validator.Validate(Empty).IsValid);
        Assert.True(validator.Validate(new SaveVitalSignsRequest("120/80", 72, 37m, 16, 70m, 175m, 98)).IsValid);
    }
}
