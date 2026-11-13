using Medilog.Application.Features.MedicalRecords;
using Xunit;

namespace Medilog.Application.Tests.Security;

public sealed class MedicalRecordAccessPolicyTests
{
    [Fact]
    public void Patient_owned_by_the_acting_doctor_is_accessible()
    {
        var doctorId = Guid.NewGuid();

        Assert.True(MedicalRecordAccessPolicy.CanAccessPatient(doctorId, doctorId));
    }

    [Fact]
    public void Patient_owned_by_another_doctor_is_not_accessible()
    {
        Assert.False(MedicalRecordAccessPolicy.CanAccessPatient(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Patient_without_an_owning_doctor_is_not_accessible()
    {
        Assert.False(MedicalRecordAccessPolicy.CanAccessPatient(null, Guid.NewGuid()));
    }

    [Fact]
    public void Empty_acting_doctor_identifier_is_never_authorized()
    {
        Assert.False(MedicalRecordAccessPolicy.CanAccessPatient(Guid.Empty, Guid.Empty));
    }
}
