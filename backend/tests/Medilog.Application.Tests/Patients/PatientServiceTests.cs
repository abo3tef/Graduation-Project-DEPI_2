using AutoMapper;
using Medilog.Application.Common;
using Medilog.Application.Common.Exceptions;
using Medilog.Application.Common.Paging;
using Medilog.Application.Features.Patients;
using Medilog.Application.Features.Patients.Requests;
using Medilog.Application.Features.Patients.Validators;
using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Medilog.Application.Tests.Patients;

/// <summary>
/// F8-C1 patient use cases on a throwaway EF Core InMemory database. Expected behavior is derived
/// from the approved rules: doctor = own patients only (BRULE-03/04), secretary = assigned doctors
/// (<c>doctor_secretaries</c>) and administrative fields only (Q-18, FR-49), delete only without
/// related records (Q-17), shared paging contract (AGENTS.md §3.1).
/// </summary>
public sealed class PatientServiceTests : IDisposable
{
    private static readonly IMapper Mapper = new MapperConfiguration(
        configuration => configuration.AddProfile<PatientsMappingProfile>(),
        NullLoggerFactory.Instance).CreateMapper();

    private readonly MedilogDbContext _db;
    private readonly FakeCurrentUser _user = new();
    private readonly Doctor _doctorA = NewDoctor("Doctor A");
    private readonly Doctor _doctorB = NewDoctor("Doctor B");
    private readonly Secretary _secretary = new() { FullName = "Secretary", AccountStatus = AccountStatus.Active };
    private readonly PatientService _service;

    public PatientServiceTests()
    {
        _db = new MedilogDbContext(new DbContextOptionsBuilder<MedilogDbContext>()
            .UseInMemoryDatabase($"medilog-f8c1-{Guid.NewGuid():N}")
            .Options);

        // The secretary is assigned to doctor A only.
        _secretary.Doctors.Add(_doctorA);
        _db.AddRange(_doctorA, _doctorB, _secretary);
        _db.SaveChanges();

        var clock = new FakeClock();
        _service = new PatientService(
            _db,
            _user,
            Mapper,
            clock,
            new CreatePatientRequestValidator(clock),
            new UpdatePatientRequestValidator(clock),
            new ListPatientsQueryValidator(),
            new PatientPickerQueryValidator());

        ActAsDoctor(_doctorA);
    }

    public void Dispose() => _db.Dispose();

    // ---------------------------------------------------------------- create

    [Fact]
    public async Task Doctor_creates_a_patient_owned_by_the_token_doctor_even_if_the_body_names_another()
    {
        var request = ValidCreate() with { DoctorId = _doctorB.Id, BloodType = BloodType.OPositive };

        var created = await _service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(_doctorA.Id, created.DoctorId);
        Assert.Equal(BloodType.OPositive, created.BloodType);
        var stored = await _db.Patients.SingleAsync();
        Assert.Equal(_doctorA.Id, stored.DoctorId);
        Assert.Equal("Sara Ali", stored.FullName);
    }

    [Fact]
    public async Task Create_trims_text_and_stores_blank_optional_fields_as_null()
    {
        var request = ValidCreate() with { FullName = "  Sara Ali  ", Phone = "  ", Address = " ", EmergencyContact = "" };

        var created = await _service.CreateAsync(request, CancellationToken.None);

        Assert.Equal("Sara Ali", created.FullName);
        Assert.Null(created.Phone);
        Assert.Null(created.Address);
        Assert.Null(created.EmergencyContact);
    }

    [Fact]
    public async Task Secretary_creates_a_patient_for_an_assigned_doctor_without_seeing_the_blood_type()
    {
        ActAsSecretary();

        var created = await _service.CreateAsync(ValidCreate() with { DoctorId = _doctorA.Id }, CancellationToken.None);

        Assert.Equal(_doctorA.Id, created.DoctorId);
        Assert.Null(created.BloodType);
        Assert.Null((await _db.Patients.SingleAsync()).BloodType);
    }

    [Fact]
    public async Task Secretary_cannot_create_a_patient_for_an_unassigned_doctor()
    {
        ActAsSecretary();

        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(ValidCreate() with { DoctorId = _doctorB.Id }, CancellationToken.None));

        Assert.Equal(ErrorCodes.DoctorNotFound, error.Code);
        Assert.Empty(_db.Patients);
    }

    [Fact]
    public async Task Secretary_must_name_the_doctor_when_creating()
    {
        ActAsSecretary();

        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.CreateAsync(ValidCreate() with { DoctorId = null }, CancellationToken.None));

        Assert.Contains(nameof(CreatePatientRequest.DoctorId), error.Errors.Keys);
    }

    [Fact]
    public async Task Secretary_cannot_set_the_blood_type_on_create()
    {
        ActAsSecretary();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.CreateAsync(
                ValidCreate() with { DoctorId = _doctorA.Id, BloodType = BloodType.APositive },
                CancellationToken.None));

        Assert.Equal(ErrorCodes.PatientMedicalFieldForbidden, error.Code);
        Assert.Empty(_db.Patients);
    }

    public static TheoryData<string, CreatePatientRequest> InvalidCreateRequests => new()
    {
        { "FullName", ValidCreate() with { FullName = "  " } },
        { "FullName", ValidCreate() with { FullName = new string('x', 201) } },
        { "DateOfBirth", ValidCreate() with { DateOfBirth = null } },
        { "DateOfBirth", ValidCreate() with { DateOfBirth = new DateOnly(2030, 1, 2) } }, // clock says 2030-01-01
        { "DateOfBirth", ValidCreate() with { DateOfBirth = new DateOnly(1899, 12, 31) } },
        { "Gender", ValidCreate() with { Gender = null } },
        { "Gender", ValidCreate() with { Gender = (Gender)99 } },
        { "Phone", ValidCreate() with { Phone = "abc" } },
        { "Phone", ValidCreate() with { Phone = "12345" } },
        { "Phone", ValidCreate() with { Phone = new string('1', 31) } },
        { "Address", ValidCreate() with { Address = new string('a', 501) } },
        { "EmergencyContact", ValidCreate() with { EmergencyContact = new string('a', 101) } },
        { "BloodType", ValidCreate() with { BloodType = (BloodType)99 } },
        { "DoctorId", ValidCreate() with { DoctorId = Guid.Empty } },
    };

    [Theory]
    [MemberData(nameof(InvalidCreateRequests))]
    public async Task Create_rejects_invalid_input_and_stores_nothing(string field, CreatePatientRequest request)
    {
        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.CreateAsync(request, CancellationToken.None));

        Assert.Contains(field, error.Errors.Keys);
        Assert.Empty(_db.Patients);
    }

    [Theory]
    [InlineData("0100 123 4567")]
    [InlineData("+20 100 123 4567")]
    [InlineData("(02) 2345-6789")]
    [InlineData(null)]
    public async Task Create_accepts_common_phone_formats(string? phone)
    {
        var created = await _service.CreateAsync(ValidCreate() with { Phone = phone }, CancellationToken.None);

        Assert.Equal(phone, created.Phone);
    }

    [Fact]
    public async Task Create_accepts_a_birth_date_of_today()
    {
        var created = await _service.CreateAsync(
            ValidCreate() with { DateOfBirth = new DateOnly(2030, 1, 1) },
            CancellationToken.None);

        Assert.Equal(new DateOnly(2030, 1, 1), created.DateOfBirth);
    }

    // ------------------------------------------------------------ identity

    [Theory]
    [InlineData(null, null)]
    [InlineData(true, null)]
    public async Task Unauthenticated_or_unknown_callers_are_forbidden(bool? hasId, UserRole? role)
    {
        _user.UserId = hasId == true ? Guid.NewGuid() : null;
        _user.Role = role;

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.GetAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(ErrorCodes.IdentityInvalid, error.Code);
    }

    [Fact]
    public async Task A_token_for_a_user_that_does_not_exist_is_forbidden()
    {
        _user.UserId = Guid.NewGuid();
        _user.Role = UserRole.Doctor;

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ListAsync(new ListPatientsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task An_inactive_doctor_is_forbidden()
    {
        _doctorA.AccountStatus = AccountStatus.Inactive;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ListAsync(new ListPatientsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task An_inactive_secretary_is_forbidden()
    {
        _secretary.AccountStatus = AccountStatus.Inactive;
        await _db.SaveChangesAsync();
        ActAsSecretary();

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.ListAsync(new ListPatientsQuery(), CancellationToken.None));
    }

    // ------------------------------------------------------------- get/scope

    [Fact]
    public async Task Doctor_reads_own_patient_with_blood_type()
    {
        var patient = await SeedPatientAsync(_doctorA, "Own", bloodType: BloodType.ABNegative);

        var response = await _service.GetAsync(patient.Id, CancellationToken.None);

        Assert.Equal(patient.Id, response.Id);
        Assert.Equal(BloodType.ABNegative, response.BloodType);
    }

    [Fact]
    public async Task Doctor_cannot_read_another_doctors_patient()
    {
        var other = await SeedPatientAsync(_doctorB, "Other");

        var error = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(other.Id, CancellationToken.None));

        Assert.Equal(ErrorCodes.PatientNotFound, error.Code);
    }

    [Fact]
    public async Task A_patient_without_an_owner_is_not_visible_to_anyone()
    {
        var orphan = new Patient { FullName = "Orphan", DoctorId = null };
        _db.Patients.Add(orphan);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(orphan.Id, CancellationToken.None));
        ActAsSecretary();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(orphan.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Secretary_reads_an_assigned_doctors_patient_but_never_the_blood_type()
    {
        var patient = await SeedPatientAsync(_doctorA, "Assigned", bloodType: BloodType.OPositive);
        ActAsSecretary();

        var response = await _service.GetAsync(patient.Id, CancellationToken.None);

        Assert.Equal("Assigned", response.FullName);
        Assert.Null(response.BloodType);
    }

    [Fact]
    public async Task Secretary_cannot_read_an_unassigned_doctors_patient()
    {
        var other = await SeedPatientAsync(_doctorB, "Other");
        ActAsSecretary();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(other.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Secretary_scope_follows_the_assignment_table()
    {
        var other = await SeedPatientAsync(_doctorB, "Other");
        ActAsSecretary();
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(other.Id, CancellationToken.None));

        _secretary.Doctors.Add(_doctorB);
        await _db.SaveChangesAsync();

        Assert.Equal(other.Id, (await _service.GetAsync(other.Id, CancellationToken.None)).Id);
    }

    // ---------------------------------------------------------------- update

    [Fact]
    public async Task Doctor_updates_own_patient_including_the_blood_type_and_keeps_the_owner()
    {
        var patient = await SeedPatientAsync(_doctorA, "Before", bloodType: BloodType.APositive);

        var updated = await _service.UpdateAsync(
            patient.Id,
            ValidUpdate() with { FullName = "After", BloodType = BloodType.BNegative },
            CancellationToken.None);

        Assert.Equal("After", updated.FullName);
        Assert.Equal(BloodType.BNegative, updated.BloodType);
        Assert.Equal(_doctorA.Id, (await _db.Patients.SingleAsync()).DoctorId);
    }

    [Fact]
    public async Task Doctor_update_without_a_blood_type_clears_it()
    {
        var patient = await SeedPatientAsync(_doctorA, "Before", bloodType: BloodType.APositive);

        await _service.UpdateAsync(patient.Id, ValidUpdate() with { BloodType = null }, CancellationToken.None);

        Assert.Null((await _db.Patients.SingleAsync()).BloodType);
    }

    [Fact]
    public async Task Doctor_cannot_update_another_doctors_patient()
    {
        var other = await SeedPatientAsync(_doctorB, "Other");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(other.Id, ValidUpdate() with { FullName = "Hacked" }, CancellationToken.None));

        Assert.Equal("Other", (await _db.Patients.SingleAsync()).FullName);
    }

    [Fact]
    public async Task Secretary_updates_administrative_fields_and_the_stored_blood_type_is_preserved()
    {
        var patient = await SeedPatientAsync(_doctorA, "Before", bloodType: BloodType.OPositive);
        ActAsSecretary();

        var updated = await _service.UpdateAsync(
            patient.Id,
            ValidUpdate() with { FullName = "After", Phone = "0100 123 4567", BloodType = null },
            CancellationToken.None);

        Assert.Equal("After", updated.FullName);
        Assert.Null(updated.BloodType);
        var stored = await _db.Patients.SingleAsync();
        Assert.Equal("After", stored.FullName);
        Assert.Equal(BloodType.OPositive, stored.BloodType);
    }

    [Fact]
    public async Task Secretary_cannot_set_the_blood_type_on_update()
    {
        var patient = await SeedPatientAsync(_doctorA, "Before", bloodType: BloodType.OPositive);
        ActAsSecretary();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.UpdateAsync(patient.Id, ValidUpdate() with { BloodType = BloodType.ANegative }, CancellationToken.None));

        Assert.Equal(ErrorCodes.PatientMedicalFieldForbidden, error.Code);
        Assert.Equal(BloodType.OPositive, (await _db.Patients.SingleAsync()).BloodType);
    }

    [Fact]
    public async Task Secretary_cannot_update_an_unassigned_doctors_patient()
    {
        var other = await SeedPatientAsync(_doctorB, "Other");
        ActAsSecretary();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(other.Id, ValidUpdate(), CancellationToken.None));
    }

    [Fact]
    public async Task Update_rejects_invalid_input_and_leaves_the_patient_unchanged()
    {
        var patient = await SeedPatientAsync(_doctorA, "Before");

        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.UpdateAsync(patient.Id, ValidUpdate() with { FullName = "", Phone = "x" }, CancellationToken.None));

        Assert.Contains("FullName", error.Errors.Keys);
        Assert.Contains("Phone", error.Errors.Keys);
        Assert.Equal("Before", (await _db.Patients.SingleAsync()).FullName);
    }

    // ------------------------------------------------------------------ list

    [Fact]
    public async Task List_returns_only_the_doctors_own_patients_with_paging_metadata()
    {
        await SeedPatientAsync(_doctorA, "Alpha");
        await SeedPatientAsync(_doctorA, "Beta");
        await SeedPatientAsync(_doctorB, "Foreign");

        var page = await _service.ListAsync(new ListPatientsQuery(), CancellationToken.None);

        Assert.Equal(["Alpha", "Beta"], page.Items.Select(item => item.FullName));
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(PagingDefaults.DefaultPageSize, page.PageSize);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task Secretary_lists_only_assigned_doctors_patients_without_blood_types()
    {
        await SeedPatientAsync(_doctorA, "Mine", bloodType: BloodType.OPositive);
        await SeedPatientAsync(_doctorB, "Foreign", bloodType: BloodType.OPositive);
        ActAsSecretary();

        var page = await _service.ListAsync(new ListPatientsQuery(), CancellationToken.None);

        var item = Assert.Single(page.Items);
        Assert.Equal("Mine", item.FullName);
        Assert.Null(item.BloodType);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task A_secretary_with_no_assigned_doctors_sees_an_empty_list()
    {
        await SeedPatientAsync(_doctorA, "Mine");
        _secretary.Doctors.Clear();
        await _db.SaveChangesAsync();
        ActAsSecretary();

        var page = await _service.ListAsync(new ListPatientsQuery(), CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task The_doctor_filter_can_narrow_but_never_widen_the_scope()
    {
        await SeedPatientAsync(_doctorA, "Mine");
        await SeedPatientAsync(_doctorB, "Foreign");
        _secretary.Doctors.Add(_doctorB);
        await _db.SaveChangesAsync();
        ActAsSecretary();

        var narrowed = await _service.ListAsync(new ListPatientsQuery(DoctorId: _doctorB.Id), CancellationToken.None);
        Assert.Equal("Foreign", Assert.Single(narrowed.Items).FullName);

        _secretary.Doctors.Remove(_doctorB);
        await _db.SaveChangesAsync();
        var widened = await _service.ListAsync(new ListPatientsQuery(DoctorId: _doctorB.Id), CancellationToken.None);
        Assert.Empty(widened.Items);

        ActAsDoctor(_doctorA);
        var asDoctor = await _service.ListAsync(new ListPatientsQuery(DoctorId: _doctorB.Id), CancellationToken.None);
        Assert.Empty(asDoctor.Items);
    }

    [Fact]
    public async Task Search_matches_name_phone_or_exact_id()
    {
        var sara = await SeedPatientAsync(_doctorA, "Sara Ali", phone: "01001112222");
        await SeedPatientAsync(_doctorA, "Omar Hassan", phone: "01113334444");
        await SeedPatientAsync(_doctorB, "Sara Foreign", phone: "01001112222");

        var byName = await _service.ListAsync(new ListPatientsQuery(Search: "Sara"), CancellationToken.None);
        var byPhone = await _service.ListAsync(new ListPatientsQuery(Search: "0111333"), CancellationToken.None);
        var byId = await _service.ListAsync(new ListPatientsQuery(Search: sara.Id.ToString()), CancellationToken.None);
        var none = await _service.ListAsync(new ListPatientsQuery(Search: "zzz"), CancellationToken.None);

        Assert.Equal(["Sara Ali"], byName.Items.Select(item => item.FullName));
        Assert.Equal(["Omar Hassan"], byPhone.Items.Select(item => item.FullName));
        Assert.Equal(sara.Id, Assert.Single(byId.Items).Id);
        Assert.Empty(none.Items);
        Assert.Equal(0, none.TotalCount);
    }

    [Fact]
    public async Task Search_by_the_id_of_another_doctors_patient_finds_nothing()
    {
        var foreign = await SeedPatientAsync(_doctorB, "Foreign");

        var page = await _service.ListAsync(new ListPatientsQuery(Search: foreign.Id.ToString()), CancellationToken.None);

        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Gender_filter_applies_at_the_data_source_and_to_the_total()
    {
        await SeedPatientAsync(_doctorA, "Female One", gender: Gender.Female);
        await SeedPatientAsync(_doctorA, "Male One", gender: Gender.Male);

        var page = await _service.ListAsync(new ListPatientsQuery(Gender: Gender.Male), CancellationToken.None);

        Assert.Equal("Male One", Assert.Single(page.Items).FullName);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Paging_returns_correct_slices_total_and_total_pages()
    {
        for (var i = 1; i <= 5; i++)
        {
            await SeedPatientAsync(_doctorA, $"Patient {i}");
        }

        var second = await _service.ListAsync(new ListPatientsQuery(PageNumber: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(["Patient 3", "Patient 4"], second.Items.Select(item => item.FullName));
        Assert.Equal(5, second.TotalCount);
        Assert.Equal(2, second.Page);
        Assert.Equal(2, second.PageSize);
        Assert.Equal(3, second.TotalPages);
    }

    [Fact]
    public async Task Ordering_is_stable_when_names_are_equal()
    {
        for (var i = 0; i < 6; i++)
        {
            await SeedPatientAsync(_doctorA, "Same Name");
        }

        var first = await _service.ListAsync(new ListPatientsQuery(PageNumber: 1, PageSize: 3), CancellationToken.None);
        var second = await _service.ListAsync(new ListPatientsQuery(PageNumber: 2, PageSize: 3), CancellationToken.None);

        var ids = first.Items.Concat(second.Items).Select(item => item.Id).ToList();
        Assert.Equal(6, ids.Distinct().Count());
        Assert.Equal(ids.OrderBy(id => id), ids);
    }

    [Theory]
    [InlineData(int.MaxValue, 50)]
    [InlineData(1000, 10)]
    public async Task A_page_past_the_end_is_empty_but_still_reports_the_total(int pageNumber, int pageSize)
    {
        await SeedPatientAsync(_doctorA, "Only");

        var page = await _service.ListAsync(new ListPatientsQuery(PageNumber: pageNumber, PageSize: pageSize), CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(pageNumber, page.Page);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    [InlineData(1, PagingDefaults.MaxPageSize + 1)]
    public async Task List_rejects_invalid_paging(int pageNumber, int pageSize)
    {
        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.ListAsync(new ListPatientsQuery(PageNumber: pageNumber, PageSize: pageSize), CancellationToken.None));

        Assert.NotEmpty(error.Errors);
    }

    [Fact]
    public async Task The_maximum_page_size_is_accepted()
    {
        var page = await _service.ListAsync(
            new ListPatientsQuery(PageSize: PagingDefaults.MaxPageSize),
            CancellationToken.None);

        Assert.Equal(PagingDefaults.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task List_rejects_an_overlong_search_and_an_undefined_gender()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.ListAsync(new ListPatientsQuery(Search: new string('a', 101)), CancellationToken.None));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.ListAsync(new ListPatientsQuery(Gender: (Gender)42), CancellationToken.None));
    }

    // ---------------------------------------------------------------- lookup

    [Fact]
    public async Task Lookup_returns_minimal_fields_for_the_callers_scope_bounded_by_the_limit()
    {
        for (var i = 1; i <= 4; i++)
        {
            await SeedPatientAsync(_doctorA, $"Patient {i}", phone: $"0100000000{i}");
        }

        await SeedPatientAsync(_doctorB, "Foreign");

        var result = await _service.LookupAsync(new PatientPickerQuery(Limit: 3), CancellationToken.None);

        Assert.Equal(["Patient 1", "Patient 2", "Patient 3"], result.Select(item => item.FullName));
        Assert.Equal("01000000001", result[0].Phone);
    }

    [Fact]
    public async Task Lookup_searches_by_name_and_a_secretary_only_reaches_assigned_doctors()
    {
        await SeedPatientAsync(_doctorA, "Sara Ali");
        await SeedPatientAsync(_doctorB, "Sara Foreign");
        ActAsSecretary();

        var result = await _service.LookupAsync(new PatientPickerQuery(Search: "Sara"), CancellationToken.None);

        Assert.Equal("Sara Ali", Assert.Single(result).FullName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PatientRules.MaxLookupLimit + 1)]
    public async Task Lookup_rejects_an_out_of_range_limit(int limit)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.LookupAsync(new PatientPickerQuery(Limit: limit), CancellationToken.None));
    }

    // ---------------------------------------------------------------- delete

    [Fact]
    public async Task Doctor_deletes_a_patient_with_no_related_records()
    {
        var patient = await SeedPatientAsync(_doctorA, "Empty");

        await _service.DeleteAsync(patient.Id, CancellationToken.None);

        Assert.Empty(_db.Patients);
    }

    public static TheoryData<string> RelatedRecordKinds =>
        ["appointment", "visit", "payment", "diagnosis", "medication", "note", "allergy", "chronic", "lab", "document"];

    [Theory]
    [MemberData(nameof(RelatedRecordKinds))]
    public async Task Delete_is_rejected_with_409_when_the_patient_has_related_records(string kind)
    {
        var patient = await SeedPatientAsync(_doctorA, "Has history");
        await SeedRelatedRecordAsync(kind, patient);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.DeleteAsync(patient.Id, CancellationToken.None));

        Assert.Equal(ErrorCodes.PatientHasRelatedRecords, error.Code);
        Assert.Single(_db.Patients);
    }

    [Fact]
    public async Task Doctor_cannot_delete_another_doctors_patient()
    {
        var other = await SeedPatientAsync(_doctorB, "Other");

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(other.Id, CancellationToken.None));
        Assert.Single(_db.Patients);
    }

    [Fact]
    public async Task Secretary_cannot_delete_a_patient()
    {
        var patient = await SeedPatientAsync(_doctorA, "Empty");
        ActAsSecretary();

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.DeleteAsync(patient.Id, CancellationToken.None));
        Assert.Single(_db.Patients);
    }

    [Fact]
    public async Task Deleting_an_unknown_patient_is_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(Guid.NewGuid(), CancellationToken.None));
    }

    // --------------------------------------------------------------- mapping

    [Fact]
    public void The_mapping_profile_is_valid()
    {
        var configuration = new MapperConfiguration(
            expression => expression.AddProfile<PatientsMappingProfile>(),
            NullLoggerFactory.Instance);

        configuration.AssertConfigurationIsValid();
    }

    // --------------------------------------------------------------- helpers

    private static Doctor NewDoctor(string name) => new()
    {
        FullName = name,
        Email = $"{Guid.NewGuid():N}@medilog.test",
        PasswordHash = "test-only-placeholder",
        AccountStatus = AccountStatus.Active,
    };

    private static CreatePatientRequest ValidCreate() => new(
        null,
        "Sara Ali",
        new DateOnly(1990, 4, 12),
        Gender.Female,
        "01001234567",
        "12 Nile Street",
        "Mother 01111111111",
        null);

    private static UpdatePatientRequest ValidUpdate() => new(
        "Sara Ali",
        new DateOnly(1990, 4, 12),
        Gender.Female,
        "01001234567",
        "12 Nile Street",
        "Mother 01111111111",
        null);

    private void ActAsDoctor(Doctor doctor) => (_user.UserId, _user.Role) = (doctor.Id, UserRole.Doctor);

    private void ActAsSecretary() => (_user.UserId, _user.Role) = (_secretary.Id, UserRole.Secretary);

    private async Task<Patient> SeedPatientAsync(
        Doctor doctor,
        string name,
        string? phone = null,
        Gender gender = Gender.Female,
        BloodType? bloodType = null)
    {
        var patient = new Patient
        {
            DoctorId = doctor.Id,
            FullName = name,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = gender,
            Phone = phone,
            BloodType = bloodType,
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        return patient;
    }

    private async Task SeedRelatedRecordAsync(string kind, Patient patient)
    {
        var doctorId = patient.DoctorId!.Value;
        var visit = new MedicalVisit
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            VisitDate = DateTimeOffset.UtcNow,
            ChiefComplaint = "Headache",
        };
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            CreatedBy = doctorId,
            AppointmentDate = new DateOnly(2030, 1, 2),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            Status = AppointmentStatus.Scheduled,
        };

        switch (kind)
        {
            case "appointment":
                _db.Appointments.Add(appointment);
                break;
            case "visit":
                _db.MedicalVisits.Add(visit);
                break;
            case "payment":
                _db.Appointments.Add(appointment);
                _db.Payments.Add(Payment.Create(appointment.Id, patient.Id, doctorId, doctorId, 100m, PaymentMethod.Cash, null));
                break;
            case "diagnosis":
                _db.MedicalVisits.Add(visit);
                _db.Diagnoses.Add(new Diagnosis
                {
                    VisitId = visit.Id,
                    PatientId = patient.Id,
                    DiagnosisName = "Migraine",
                    DiagnosisDate = new DateOnly(2030, 1, 1),
                });
                break;
            case "medication":
                _db.Medications.Add(new Medication
                {
                    VisitId = Guid.NewGuid(),
                    PatientId = patient.Id,
                    MedicationName = "Amoxicillin",
                    Dosage = "500 mg",
                    Frequency = "Daily",
                    Duration = "7 days",
                });
                break;
            case "note":
                _db.MedicalNotes.Add(new MedicalNote { PatientId = patient.Id, DoctorId = doctorId, NoteContent = "note" });
                break;
            case "allergy":
                _db.Allergies.Add(new Allergy { PatientId = patient.Id, AllergyName = "Pollen" });
                break;
            case "chronic":
                _db.ChronicConditions.Add(new ChronicCondition { PatientId = patient.Id, ConditionName = "Asthma" });
                break;
            case "lab":
                _db.LaboratoryResults.Add(new LaboratoryResult { PatientId = patient.Id, TestName = "CBC", Unit = "g/dL", Value = 13m });
                break;
            case "document":
                _db.MedicalDocuments.Add(new MedicalDocument { PatientId = patient.Id, UploadedBy = doctorId, FileName = "scan.pdf", Location = "files/scan.pdf" });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        await _db.SaveChangesAsync();
    }
}
