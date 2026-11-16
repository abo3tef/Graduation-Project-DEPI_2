using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Medilog.Application.Common.Security;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;
using Medilog.WebAPI.Tests.Support;
using Xunit;

namespace Medilog.WebAPI.Tests.Api;

/// <summary>
/// End-to-end tests for the payment routes (F7). The real WebAPI pipeline runs on
/// top of <see cref="WebApiFactory"/>, so authentication, role authorization,
/// routing, JSON contracts and the global exception handler are all exercised.
/// </summary>
/// <remarks>
/// Every test owns an isolated in-memory database. Two doctors, two patients, two
/// appointments and one secretary assigned to doctor A model the approved scope:
/// a secretary reaches payments of assigned doctors only, a doctor only their own.
/// </remarks>
public sealed class PaymentsApiTests
{
    private const string Base = "/api/payments";

    [Fact]
    public async Task Create_returns_201_with_a_pending_payment_that_can_be_read_back()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act
        var response = await client.PostAsJsonAsync(
            Base,
            CreateBody(scope.AppointmentA, amount: 250.50m, method: "Card", notes: "first visit"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadJsonAsync();
        var paymentId = created.GetProperty("id").GetGuid();

        Assert.NotEqual(Guid.Empty, paymentId);
        Assert.Equal(scope.AppointmentA, created.GetProperty("appointmentId").GetGuid());
        Assert.Equal(scope.PatientA, created.GetProperty("patientId").GetGuid());
        Assert.Equal(scope.DoctorA, created.GetProperty("doctorId").GetGuid());
        Assert.Equal("Alpha Patient", created.GetProperty("patientName").GetString());
        Assert.Equal(250.50m, created.GetProperty("amount").GetDecimal());
        Assert.Equal("Card", created.GetProperty("method").GetString());
        Assert.Equal("Pending", created.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("paidAt").ValueKind);
        Assert.Equal("first visit", created.GetProperty("notes").GetString());

        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"{Base}/{paymentId}", response.Headers.Location!.AbsolutePath);

        var fetched = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(paymentId, (await fetched.ReadJsonAsync()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task The_secretary_lists_only_the_payments_of_assigned_doctors()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        await factory.SeedAsync(db =>
        {
            db.Add(SeedPayment(scope, scope.DoctorA, scope.AppointmentA));
            db.Add(SeedPayment(scope, scope.DoctorB, scope.AppointmentB));
        });

        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        // Act
        var response = await client.GetAsync(Base);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.ReadJsonAsync();
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(10, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, page.GetProperty("totalPages").GetInt32());

        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(scope.DoctorA, item.GetProperty("doctorId").GetGuid());
        Assert.Equal(scope.AppointmentA, item.GetProperty("appointmentId").GetGuid());
    }

    [Fact]
    public async Task List_supports_search_filters_and_paging()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        var secondAppointment = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            var first = SeedPayment(scope, scope.DoctorA, scope.AppointmentA);
            first.Status = PaymentStatus.Paid;
            first.PaidAtUtc = new DateTimeOffset(2030, 1, 2, 9, 0, 0, TimeSpan.Zero);
            db.Add(first);

            var second = SeedPayment(scope, scope.DoctorA, secondAppointment);
            second.Status = PaymentStatus.Partial;
            db.Add(second);

            db.Add(new Appointment
            {
                Id = secondAppointment,
                PatientId = scope.PatientA,
                DoctorId = scope.DoctorA,
                CreatedBy = scope.DoctorA,
                AppointmentDate = new DateOnly(2030, 1, 5),
                StartTime = new TimeOnly(11, 0),
                EndTime = new TimeOnly(11, 30),
                Status = AppointmentStatus.Scheduled,
            });
        });

        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act
        var paid = await client.GetAsync($"{Base}?status=Paid");
        var byName = await client.GetAsync($"{Base}?search=Alpha");
        var byMissingName = await client.GetAsync($"{Base}?search=Beta");
        var paged = await client.GetAsync($"{Base}?pageNumber=2&pageSize=1");
        var unknownDoctor = await client.GetAsync($"{Base}?doctorId={scope.DoctorB}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var paidPage = await paid.ReadJsonAsync();
        Assert.Equal(1, paidPage.GetProperty("totalCount").GetInt32());
        Assert.Equal("Paid", Assert.Single(paidPage.GetProperty("items").EnumerateArray())
            .GetProperty("status").GetString());

        Assert.Equal(2, (await byName.ReadJsonAsync()).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await byMissingName.ReadJsonAsync()).GetProperty("totalCount").GetInt32());

        var secondPage = await paged.ReadJsonAsync();
        Assert.Equal(2, secondPage.GetProperty("page").GetInt32());
        Assert.Equal(1, secondPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, secondPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, secondPage.GetProperty("totalPages").GetInt32());
        Assert.Single(secondPage.GetProperty("items").EnumerateArray());

        // A client-supplied doctor filter can only narrow the server-side scope.
        Assert.Equal(0, (await unknownDoctor.ReadJsonAsync()).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected_with_401_on_every_route()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClient();

        // Act & Assert
        foreach (var request in BuildRouteRequests())
        {
            using var message = request.ToHttpRequestMessage();
            var response = await client.SendAsync(message);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Requests_without_an_allowed_role_are_rejected_with_403_on_every_route()
    {
        // Arrange
        using var factory = new WebApiFactory();
        using var client = factory.CreateClientFor(Guid.NewGuid(), "Admin");

        // Act & Assert
        foreach (var request in BuildRouteRequests())
        {
            using var message = request.ToHttpRequestMessage();
            var response = await client.SendAsync(message);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_secretary_cannot_read_or_change_a_payment_of_an_unassigned_doctor()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        var paymentId = Guid.NewGuid();
        await factory.SeedAsync(db => db.Add(SeedPayment(scope, scope.DoctorB, scope.AppointmentB, paymentId)));

        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        // Act
        var get = await client.GetAsync($"{Base}/{paymentId}");
        var update = await client.PutAsJsonAsync(
            $"{Base}/{paymentId}",
            new { amount = 10m, method = "Cash", notes = (string?)null });
        var status = await client.PutAsJsonAsync($"{Base}/{paymentId}/status", new { status = "Paid" });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal("payment.not_found", (await get.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_doctor_cannot_read_a_payment_of_another_doctor()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        var paymentId = Guid.NewGuid();
        await factory.SeedAsync(db => db.Add(SeedPayment(scope, scope.DoctorB, scope.AppointmentB, paymentId)));

        using var otherDoctor = factory.CreateClientFor(scope.DoctorA);
        using var owningDoctor = factory.CreateClientFor(scope.DoctorB);

        // Act
        var forbidden = await otherDoctor.GetAsync($"{Base}/{paymentId}");
        var allowed = await owningDoctor.GetAsync($"{Base}/{paymentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(scope.DoctorB, (await allowed.ReadJsonAsync()).GetProperty("doctorId").GetGuid());
    }

    [Fact]
    public async Task A_secretary_cannot_record_a_payment_for_an_unassigned_doctor()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        // Act
        var response = await client.PostAsJsonAsync(Base, CreateBody(scope.AppointmentB));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "payment.appointment_not_found",
            (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_second_payment_for_the_same_appointment_returns_409()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);
        var body = CreateBody(scope.AppointmentA);

        var first = await client.PostAsJsonAsync(Base, body);

        // Act
        var second = await client.PostAsJsonAsync(Base, body);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(
            "payment.already_exists",
            (await second.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Status_moves_through_partial_to_paid_and_records_the_paid_time()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);
        var created = await client.PostAsJsonAsync(Base, CreateBody(scope.AppointmentA));
        var paymentId = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        // Act
        var partial = await client.PutAsJsonAsync($"{Base}/{paymentId}/status", new { status = "Partial" });
        var paid = await client.PutAsJsonAsync($"{Base}/{paymentId}/status", new { status = "Paid" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, partial.StatusCode);
        var partialJson = await partial.ReadJsonAsync();
        Assert.Equal("Partial", partialJson.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, partialJson.GetProperty("paidAt").ValueKind);

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var paidJson = await paid.ReadJsonAsync();
        Assert.Equal("Paid", paidJson.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, paidJson.GetProperty("paidAt").ValueKind);
    }

    [Fact]
    public async Task Re_applying_the_current_status_returns_409_with_the_payment_code()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);
        var created = await client.PostAsJsonAsync(Base, CreateBody(scope.AppointmentA));
        var paymentId = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        // Act
        var response = await client.PutAsJsonAsync($"{Base}/{paymentId}/status", new { status = "Pending" });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "payment.invalid_status_transition",
            (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_paid_payment_cannot_be_edited_or_moved_back()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        var paymentId = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            var paid = SeedPayment(scope, scope.DoctorA, scope.AppointmentA, paymentId);
            paid.Status = PaymentStatus.Paid;
            paid.PaidAtUtc = new DateTimeOffset(2030, 1, 2, 9, 0, 0, TimeSpan.Zero);
            db.Add(paid);
        });

        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act
        var update = await client.PutAsJsonAsync(
            $"{Base}/{paymentId}",
            new { amount = 10m, method = "Cash", notes = (string?)null });
        var status = await client.PutAsJsonAsync($"{Base}/{paymentId}/status", new { status = "Partial" });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal("payment.paid_final", (await update.ReadJsonAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.Conflict, status.StatusCode);
        Assert.Equal(
            "payment.invalid_status_transition",
            (await status.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Updating_a_pending_payment_changes_only_its_details()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        var paymentId = Guid.NewGuid();
        await factory.SeedAsync(db => db.Add(SeedPayment(scope, scope.DoctorA, scope.AppointmentA, paymentId)));

        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        // Act
        var response = await client.PutAsJsonAsync(
            $"{Base}/{paymentId}",
            new { amount = 999.99m, method = "Card", notes = " corrected " });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.ReadJsonAsync();
        Assert.Equal(paymentId, updated.GetProperty("id").GetGuid());
        Assert.Equal(999.99m, updated.GetProperty("amount").GetDecimal());
        Assert.Equal("Card", updated.GetProperty("method").GetString());
        Assert.Equal("corrected", updated.GetProperty("notes").GetString());
        Assert.Equal("Pending", updated.GetProperty("status").GetString());
        Assert.Equal(scope.AppointmentA, updated.GetProperty("appointmentId").GetGuid());
        Assert.Equal(scope.DoctorA, updated.GetProperty("doctorId").GetGuid());
    }

    [Fact]
    public async Task An_unknown_payment_returns_404_with_the_payment_code()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act
        var response = await client.GetAsync($"{Base}/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("payment.not_found", (await response.ReadJsonAsync()).GetProperty("code").GetString());

        var raw = (await response.ReadJsonAsync()).GetRawText();
        Assert.DoesNotContain("stackTrace", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Payable_appointments_are_scoped_and_exclude_billed_appointments()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        var billed = Guid.NewGuid();
        var unbilled = Guid.NewGuid();
        await factory.SeedAsync(db =>
        {
            db.Add(new Appointment
            {
                Id = billed,
                PatientId = scope.PatientA,
                DoctorId = scope.DoctorA,
                CreatedBy = scope.DoctorA,
                AppointmentDate = new DateOnly(2030, 1, 3),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 30),
                Status = AppointmentStatus.Scheduled,
            });
            db.Add(new Appointment
            {
                Id = unbilled,
                PatientId = scope.PatientA,
                DoctorId = scope.DoctorA,
                CreatedBy = scope.DoctorA,
                AppointmentDate = new DateOnly(2030, 1, 4),
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(10, 30),
                Status = AppointmentStatus.Scheduled,
            });
            db.Add(SeedPayment(scope, scope.DoctorA, billed));
        });

        using var client = factory.CreateClientFor(scope.Secretary, Roles.Secretary);

        // Act
        var response = await client.GetAsync($"{Base}/payable-appointments");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == billed);
        Assert.Contains(items, item => item.GetProperty("id").GetGuid() == unbilled);
        Assert.All(items, item =>
        {
            Assert.Equal(scope.DoctorA, item.GetProperty("doctorId").GetGuid());
            Assert.Equal("Alpha Patient", item.GetProperty("patientName").GetString());
        });

        // The unassigned doctor's appointments never reach the secretary.
        Assert.All(items, item => Assert.NotEqual(scope.AppointmentB, item.GetProperty("id").GetGuid()));
    }

    [Fact]
    public async Task Payable_appointments_exclude_cancelled_and_no_show()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        var eligible = new List<Guid>
        {
            Guid.NewGuid(), // Scheduled
            Guid.NewGuid(), // Confirmed
            Guid.NewGuid(), // Completed
        };
        var cancelled = Guid.NewGuid();
        var noShow = Guid.NewGuid();

        await factory.SeedAsync(db =>
        {
            var day = 10;
            void Add(Guid id, AppointmentStatus status)
            {
                db.Add(new Appointment
                {
                    Id = id,
                    PatientId = scope.PatientA,
                    DoctorId = scope.DoctorA,
                    CreatedBy = scope.DoctorA,
                    AppointmentDate = new DateOnly(2030, 1, day++),
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(9, 30),
                    Status = status,
                });
            }

            Add(eligible[0], AppointmentStatus.Scheduled);
            Add(eligible[1], AppointmentStatus.Confirmed);
            Add(eligible[2], AppointmentStatus.Completed);
            Add(cancelled, AppointmentStatus.Cancelled);
            Add(noShow, AppointmentStatus.NoShow);
        });

        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act
        var response = await client.GetAsync($"{Base}/payable-appointments");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ids = (await response.ReadJsonAsync())
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToHashSet();

        // The fixture's own Scheduled appointment is still payable.
        Assert.Contains(scope.AppointmentA, ids);
        Assert.All(eligible, id => Assert.Contains(id, ids));
        Assert.DoesNotContain(cancelled, ids);
        Assert.DoesNotContain(noShow, ids);
    }

    [Fact]
    public async Task Payable_appointments_respect_the_bound_limit()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act
        var response = await client.GetAsync($"{Base}/payable-appointments?limit=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task A_validation_error_returns_400_problem_details_without_internal_details()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act
        var response = await client.PostAsJsonAsync(Base, CreateBody(scope.AppointmentA, amount: 0m));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.ReadJsonAsync();
        Assert.Equal("validation.failed", problem.GetProperty("code").GetString());
        Assert.True(problem.GetProperty("errors").TryGetProperty("amount", out _));

        var raw = problem.GetRawText();
        Assert.DoesNotContain("stackTrace", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_client_cannot_set_the_status_method_precision_or_ownership_fields()
    {
        // Arrange
        using var factory = new WebApiFactory();
        var scope = await SeedScopeAsync(factory);
        using var client = factory.CreateClientFor(scope.DoctorA);

        // Act: extra members are ignored by model binding; amount keeps two decimals.
        var response = await client.PostAsJsonAsync(Base, new
        {
            appointmentId = scope.AppointmentA,
            amount = 100.50m,
            method = "Cash",
            notes = "ok",
            status = "Paid",
            doctorId = scope.DoctorB,
            patientId = scope.PatientB,
            createdBy = Guid.NewGuid(),
            paidAt = DateTimeOffset.UtcNow,
        });

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.ReadJsonAsync();
        Assert.Equal("Pending", created.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("paidAt").ValueKind);
        Assert.Equal(scope.DoctorA, created.GetProperty("doctorId").GetGuid());
        Assert.Equal(scope.PatientA, created.GetProperty("patientId").GetGuid());
        Assert.Equal(100.50m, created.GetProperty("amount").GetDecimal());
    }

    private static Payment SeedPayment(
        Scope scope,
        Guid doctorId,
        Guid appointmentId,
        Guid? paymentId = null) => new()
    {
        Id = paymentId ?? Guid.NewGuid(),
        DoctorId = doctorId,
        PatientId = doctorId == scope.DoctorA ? scope.PatientA : scope.PatientB,
        AppointmentId = appointmentId,
        CreatedBy = scope.Secretary,
        Amount = 500m,
        Method = PaymentMethod.Cash,
        Status = PaymentStatus.Pending,
    };

    private static object CreateBody(
        Guid appointmentId,
        decimal amount = 500m,
        string method = "Cash",
        string? notes = null) => new
    {
        appointmentId,
        amount,
        method,
        notes,
    };

    private static async Task<Scope> SeedScopeAsync(WebApiFactory factory)
    {
        var scope = new Scope(
            DoctorA: Guid.NewGuid(),
            DoctorB: Guid.NewGuid(),
            PatientA: Guid.NewGuid(),
            PatientB: Guid.NewGuid(),
            AppointmentA: Guid.NewGuid(),
            AppointmentB: Guid.NewGuid(),
            Secretary: Guid.NewGuid());

        await factory.SeedAsync(db =>
        {
            var doctorA = ApiData.Doctor(scope.DoctorA);
            var doctorB = ApiData.Doctor(scope.DoctorB);
            var patientA = LocalPatient(scope.PatientA, doctorA.Id, "Alpha Patient");
            var patientB = LocalPatient(scope.PatientB, doctorB.Id, "Beta Patient");
            var secretary = new Secretary
            {
                Id = scope.Secretary,
                FullName = "Secretary Under Test",
                Email = $"{Guid.NewGuid():N}@medilog.test",
                PasswordHash = "test-only-placeholder",
                AccountStatus = AccountStatus.Active,
                Doctors = [doctorA],
            };

            db.AddRange(
                doctorA,
                doctorB,
                patientA,
                patientB,
                ApiData.Appointment(scope.AppointmentA, patientA.Id, doctorA.Id),
                ApiData.Appointment(scope.AppointmentB, patientB.Id, doctorB.Id),
                secretary);
        });

        return scope;
    }

    private static Patient LocalPatient(Guid id, Guid doctorId, string name) => new()
    {
        Id = id,
        DoctorId = doctorId,
        FullName = name,
        DateOfBirth = new DateOnly(1990, 4, 12),
        Gender = Gender.Female,
    };

    private static IEnumerable<RouteRequest> BuildRouteRequests()
    {
        var id = Guid.NewGuid();
        var paymentBody = new { appointmentId = Guid.NewGuid(), amount = 10m, method = "Cash", notes = "x" };
        var statusBody = new { status = "Paid" };

        yield return new RouteRequest(HttpMethod.Post, Base, JsonContent.Create(paymentBody));
        yield return new RouteRequest(HttpMethod.Get, Base, null);
        yield return new RouteRequest(HttpMethod.Get, $"{Base}/{id}", null);
        yield return new RouteRequest(HttpMethod.Put, $"{Base}/{id}", JsonContent.Create(paymentBody));
        yield return new RouteRequest(HttpMethod.Put, $"{Base}/{id}/status", JsonContent.Create(statusBody));
        yield return new RouteRequest(HttpMethod.Get, $"{Base}/payable-appointments", null);
    }

    private sealed record Scope(
        Guid DoctorA,
        Guid DoctorB,
        Guid PatientA,
        Guid PatientB,
        Guid AppointmentA,
        Guid AppointmentB,
        Guid Secretary);

    private sealed record RouteRequest(HttpMethod Method, string Url, HttpContent? Content)
    {
        public HttpRequestMessage ToHttpRequestMessage() => new(Method, Url)
        {
            Content = Content,
        };
    }
}
