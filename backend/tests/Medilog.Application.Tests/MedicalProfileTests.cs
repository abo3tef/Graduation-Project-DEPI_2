using System.Globalization;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Tests;

/// <summary>
/// The medical profile contract: every Class Spec §4.2 Patient attribute
/// (including <c>BloodType</c> and <c>EmergencyContact</c>), the derived age,
/// empty collections, and the deterministic collection ordering (task file §4.3).
/// </summary>
public class MedicalProfileTests
{
    [Fact]
    public async Task Profile_returns_the_class_specification_patient_attributes()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        var profile = await s.ProfileAsync(s.PatientA);

        Assert.Equal(s.PatientA.Id, profile.PatientId);
        Assert.Equal("Patient A", profile.FullName);
        Assert.Equal(new DateOnly(2010, 6, 15), profile.DateOfBirth);
        Assert.Equal(19, profile.Age); // clock defaults to 2030-01-01
        Assert.Equal(Gender.Male, profile.Gender);
        Assert.Equal("0100", profile.Phone);
        Assert.Equal("Cairo", profile.Address);
        Assert.Equal(BloodType.APositive, profile.BloodType);
        Assert.Equal("0122-9999", profile.EmergencyContact);
    }

    [Fact]
    public async Task Profile_handles_missing_optional_values()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorB);

        var profile = await s.ProfileAsync(s.PatientB);

        Assert.Null(profile.BloodType);
        Assert.Null(profile.Phone);
        Assert.Null(profile.Address);
        Assert.Null(profile.EmergencyContact);
    }

    [Theory]
    [InlineData("2030-06-14", 19)] // day before the birthday
    [InlineData("2030-06-15", 20)] // on the birthday
    [InlineData("2030-06-16", 20)] // after the birthday
    public async Task Age_is_correct_around_the_birthday(string today, int expectedAge)
    {
        var s = new MedicalRecordScenario();
        s.Clock.LocalNow = DateTime.Parse(today, CultureInfo.InvariantCulture);
        s.ActAsDoctor(s.DoctorA);

        var profile = await s.ProfileAsync(s.PatientA); // born 2010-06-15

        Assert.Equal(expectedAge, profile.Age);
    }

    [Theory]
    [InlineData("2030-02-28", 29)] // non-leap year, day before Feb 29
    [InlineData("2030-03-01", 30)] // non-leap year, after Feb 29
    [InlineData("2032-02-29", 32)] // leap year, on the birthday
    public async Task Age_is_correct_for_a_feb_29_birth_date(string today, int expectedAge)
    {
        var s = new MedicalRecordScenario();
        s.Clock.LocalNow = DateTime.Parse(today, CultureInfo.InvariantCulture);
        s.ActAsDoctor(s.DoctorA);

        var profile = await s.ProfileAsync(s.LeapPatient); // born 2000-02-29

        Assert.Equal(expectedAge, profile.Age);
    }

    [Fact]
    public async Task Profile_returns_empty_collections_when_there_are_no_records()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorB);

        var profile = await s.ProfileAsync(s.PatientB);

        Assert.Empty(profile.Allergies);
        Assert.Empty(profile.ChronicConditions);
    }

    [Fact]
    public async Task Allergies_are_ordered_by_severity_then_name()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        // Inserted deliberately out of order; the fake store preserves order.
        s.Store.Allergies.AddRange(
        [
            new Allergy { PatientId = s.PatientA.Id, AllergyName = "Peanuts", Severity = AllergySeverity.Mild },
            new Allergy { PatientId = s.PatientA.Id, AllergyName = "Zinc", Severity = AllergySeverity.Severe },
            new Allergy { PatientId = s.PatientA.Id, AllergyName = "Aspirin", Severity = AllergySeverity.Severe },
            new Allergy { PatientId = s.PatientA.Id, AllergyName = "Latex", Severity = AllergySeverity.Moderate },
            new Allergy { PatientId = s.PatientA.Id, AllergyName = "Dust", Severity = null }
        ]);

        var profile = await s.ProfileAsync(s.PatientA);

        Assert.Equal(
            ["Aspirin", "Zinc", "Latex", "Peanuts", "Dust"],
            profile.Allergies.Select(allergy => allergy.Name).ToArray());
    }

    [Fact]
    public async Task Conditions_are_ordered_by_status_then_newest_diagnosis_date_with_nulls_last()
    {
        var s = new MedicalRecordScenario();
        s.ActAsDoctor(s.DoctorA);

        s.Store.Conditions.AddRange(
        [
            new ChronicCondition
            {
                PatientId = s.PatientA.Id,
                ConditionName = "Asthma",
                Status = ChronicConditionStatus.Resolved,
                DiagnosisDate = new DateOnly(2020, 1, 1)
            },
            new ChronicCondition { PatientId = s.PatientA.Id, ConditionName = "Diabetes", Status = ChronicConditionStatus.Active },
            new ChronicCondition
            {
                PatientId = s.PatientA.Id,
                ConditionName = "Hypertension",
                Status = ChronicConditionStatus.Controlled,
                DiagnosisDate = new DateOnly(2022, 5, 1)
            },
            new ChronicCondition
            {
                PatientId = s.PatientA.Id,
                ConditionName = "Anemia",
                Status = ChronicConditionStatus.Active,
                DiagnosisDate = new DateOnly(2021, 3, 1)
            }
        ]);

        var profile = await s.ProfileAsync(s.PatientA);

        // Active first (newest date before the undated one), then Controlled, then Resolved.
        Assert.Equal(
            ["Anemia", "Diabetes", "Hypertension", "Asthma"],
            profile.ChronicConditions.Select(condition => condition.Name).ToArray());
    }
}
