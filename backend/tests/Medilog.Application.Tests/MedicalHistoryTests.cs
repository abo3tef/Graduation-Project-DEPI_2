using Medilog.Application.Tests.Support;
using Medilog.Domain.Entities;
using Medilog.Domain.Enums;

namespace Medilog.Application.Tests;

/// <summary>
/// The medical history contract (task file §4.4): previous visits of the
/// patient only, newest first with a stable tie-breaker, correct totals, each
/// visit holding only its own (and its patient's) records, records authored by
/// another doctor included, unlinked notes/labs and documents absent, and the
/// deterministic child ordering.
/// </summary>
public class MedicalHistoryTests
{
    private static readonly DateTimeOffset Jan10 = new(2030, 1, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jan20 = new(2030, 1, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jan25 = new(2030, 1, 25, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Doctor A owns Patient A; visits V1 (Jan 10) and V2+V3 (Jan 20, same instant).</summary>
    private static (MedicalVisit v1, MedicalVisit v2, MedicalVisit v3) SeedVisits(MedicalRecordScenario s)
    {
        var v1 = s.SeedVisit(s.PatientA, s.DoctorA, Jan10, "Sore throat");
        var v2 = s.SeedVisit(s.PatientA, s.DoctorB, Jan20, "Follow-up"); // authored by another doctor
        var v3 = s.SeedVisit(s.PatientA, s.DoctorA, Jan20); // no children at all

        return (v1, v2, v3);
    }

    [Fact]
    public async Task History_returns_only_the_patients_visits_newest_first()
    {
        var s = new MedicalRecordScenario();
        var (v1, v2, v3) = SeedVisits(s);
        s.SeedVisit(s.PatientB, s.DoctorB, Jan25); // another patient's visit
        s.ActAsDoctor(s.DoctorA);

        var page = await s.HistoryAsync(s.PatientA, page: 1, pageSize: 10);

        Assert.Equal(3, page.TotalCount);
        var ids = page.Items.Select(visit => visit.VisitId).ToArray();

        // The two equal-instant visits occupy the first two slots, ordered by the
        // id tie-breaker; the older visit is last.
        var expected = new[] { v2.Id, v3.Id }.OrderDescending().Append(v1.Id).ToArray();
        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task History_pages_bounded_with_correct_totals()
    {
        var s = new MedicalRecordScenario();
        SeedVisits(s);
        s.ActAsDoctor(s.DoctorA);

        var page1 = await s.HistoryAsync(s.PatientA, page: 1, pageSize: 2);
        var page2 = await s.HistoryAsync(s.PatientA, page: 2, pageSize: 2);
        var page3 = await s.HistoryAsync(s.PatientA, page: 3, pageSize: 2);

        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Empty(page3.Items);
        Assert.All(new[] { page1, page2, page3 }, page => Assert.Equal(3, page.TotalCount));

        // Pages do not repeat items.
        var all = page1.Items.Concat(page2.Items).Select(visit => visit.VisitId).ToArray();
        Assert.Equal(all.Length, all.Distinct().Count());
    }

    [Fact]
    public async Task Visit_with_no_children_has_empty_collections()
    {
        var s = new MedicalRecordScenario();
        var (_, _, v3) = SeedVisits(s);
        s.ActAsDoctor(s.DoctorA);

        var page = await s.HistoryAsync(s.PatientA);

        var visit = page.Items.Single(item => item.VisitId == v3.Id);

        Assert.Empty(visit.Diagnoses);
        Assert.Empty(visit.Medications);
        Assert.Empty(visit.VitalSigns);
        Assert.Empty(visit.Notes);
        Assert.Empty(visit.LabResults);
    }

    [Fact]
    public async Task Each_visit_holds_only_its_own_children_and_vitals_cannot_cross_patients()
    {
        var s = new MedicalRecordScenario();
        var (v1, v2, _) = SeedVisits(s);

        s.Store.Diagnoses.Add(new Diagnosis
        {
            VisitId = v1.Id,
            PatientId = s.PatientA.Id,
            DiagnosisName = "Flu",
            DiagnosisDate = new DateOnly(2029, 12, 1)
        });
        s.Store.Diagnoses.Add(new Diagnosis
        {
            VisitId = v2.Id,
            PatientId = s.PatientA.Id,
            DiagnosisName = "Migraine",
            DiagnosisDate = new DateOnly(2030, 1, 20)
        });
        s.Store.Medications.Add(new Medication
        {
            VisitId = v1.Id,
            PatientId = s.PatientA.Id,
            MedicationName = "Amoxicillin",
            Dosage = "500mg",
            Frequency = "3x/day"
        });
        s.Store.VitalSigns.Add(new VitalSigns
        {
            VisitId = v1.Id,
            HeartRate = 72,
            Temperature = 37.5m
        });
        s.Store.Notes.Add(new MedicalNote
        {
            VisitId = v1.Id,
            PatientId = s.PatientA.Id,
            DoctorId = s.DoctorB.Id,
            NoteContent = "Patient responded well",
            CreatedAtUtc = Jan10
        });
        s.Store.LabResults.Add(new LaboratoryResult
        {
            VisitId = v1.Id,
            PatientId = s.PatientA.Id,
            TestName = "CBC",
            TestDate = new DateOnly(2030, 1, 8),
            Value = 12.5m,
            Unit = "g/dL"
        });

        // A vital-signs row on ANOTHER patient's visit: the table has no patient
        // column, so only the visit scoping can keep it out.
        var otherVisit = s.SeedVisit(s.PatientB, s.DoctorB, Jan25);
        s.Store.VitalSigns.Add(new VitalSigns
        {
            VisitId = otherVisit.Id,
            HeartRate = 120
        });

        s.ActAsDoctor(s.DoctorA);
        var page = await s.HistoryAsync(s.PatientA);

        var first = page.Items.Single(item => item.VisitId == v1.Id);
        var second = page.Items.Single(item => item.VisitId == v2.Id);

        Assert.Single(first.Diagnoses);
        Assert.Equal("Flu", first.Diagnoses.Single().Name);
        Assert.Single(first.Medications);
        Assert.Single(first.VitalSigns);
        Assert.Equal(72, first.VitalSigns.Single().HeartRate); // never the other patient's row
        Assert.Single(first.Notes);
        Assert.Single(first.LabResults);

        Assert.Single(second.Diagnoses);
        Assert.Equal("Migraine", second.Diagnoses.Single().Name);
        Assert.Empty(second.Medications);
        Assert.Empty(second.VitalSigns);
        Assert.Empty(second.Notes);
        Assert.Empty(second.LabResults);
    }

    [Fact]
    public async Task Records_authored_by_another_doctor_are_included_with_their_author()
    {
        var s = new MedicalRecordScenario();
        var (v1, v2, _) = SeedVisits(s); // v2 is authored by Doctor B

        s.Store.Notes.Add(new MedicalNote
        {
            VisitId = v1.Id,
            PatientId = s.PatientA.Id,
            DoctorId = s.DoctorB.Id,
            NoteContent = "Second opinion",
            CreatedAtUtc = Jan10
        });

        s.ActAsDoctor(s.DoctorA);
        var page = await s.HistoryAsync(s.PatientA);

        var visitB = page.Items.Single(item => item.VisitId == v2.Id);
        Assert.Equal(s.DoctorB.Id, visitB.DoctorId);
        Assert.Equal("Doctor B", visitB.DoctorName);

        var note = page.Items.Single(item => item.VisitId == v1.Id).Notes.Single();
        Assert.Equal(s.DoctorB.Id, note.DoctorId);
        Assert.Equal("Doctor B", note.DoctorName);
    }

    [Fact]
    public async Task Unlinked_notes_and_labs_are_never_returned()
    {
        var s = new MedicalRecordScenario();
        SeedVisits(s);

        s.Store.Notes.Add(new MedicalNote
        {
            VisitId = null, // patient-level note: F3-C2 scope
            PatientId = s.PatientA.Id,
            DoctorId = s.DoctorA.Id,
            NoteContent = "General remark",
            CreatedAtUtc = Jan10
        });
        s.Store.LabResults.Add(new LaboratoryResult
        {
            VisitId = null, // patient-level result: F3-C2 scope
            PatientId = s.PatientA.Id,
            TestName = "Standalone panel",
            TestDate = new DateOnly(2030, 1, 2),
            Value = 1m,
            Unit = "mg/L"
        });

        s.ActAsDoctor(s.DoctorA);
        var page = await s.HistoryAsync(s.PatientA);

        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, visit => Assert.Empty(visit.Notes));
        Assert.All(page.Items, visit => Assert.Empty(visit.LabResults));
    }

    [Fact]
    public async Task Child_collections_are_ordered_deterministically()
    {
        var s = new MedicalRecordScenario();
        var (v1, _, _) = SeedVisits(s);

        // Diagnoses: newest date first, then name for equal dates.
        s.Store.Diagnoses.AddRange(
        [
            new Diagnosis { VisitId = v1.Id, PatientId = s.PatientA.Id, DiagnosisName = "Flu", DiagnosisDate = new DateOnly(2029, 12, 1) },
            new Diagnosis { VisitId = v1.Id, PatientId = s.PatientA.Id, DiagnosisName = "Cold", DiagnosisDate = new DateOnly(2030, 1, 5) },
            new Diagnosis { VisitId = v1.Id, PatientId = s.PatientA.Id, DiagnosisName = "Asthma", DiagnosisDate = new DateOnly(2030, 1, 5) }
        ]);

        // Medications: by name.
        s.Store.Medications.AddRange(
        [
            new Medication { VisitId = v1.Id, PatientId = s.PatientA.Id, MedicationName = "Zinc", Dosage = "50mg", Frequency = "1x/day" },
            new Medication { VisitId = v1.Id, PatientId = s.PatientA.Id, MedicationName = "Amoxicillin", Dosage = "500mg", Frequency = "3x/day" }
        ]);

        // Notes: newest created first.
        s.Store.Notes.AddRange(
        [
            new MedicalNote { VisitId = v1.Id, PatientId = s.PatientA.Id, DoctorId = s.DoctorA.Id, NoteContent = "older", CreatedAtUtc = Jan10 },
            new MedicalNote { VisitId = v1.Id, PatientId = s.PatientA.Id, DoctorId = s.DoctorA.Id, NoteContent = "newer", CreatedAtUtc = Jan25 }
        ]);

        // Labs: newest test date first, then name for equal dates.
        s.Store.LabResults.AddRange(
        [
            new LaboratoryResult { VisitId = v1.Id, PatientId = s.PatientA.Id, TestName = "Urinalysis", TestDate = new DateOnly(2030, 1, 1), Value = 1m, Unit = "mg/L" },
            new LaboratoryResult { VisitId = v1.Id, PatientId = s.PatientA.Id, TestName = "CBC", TestDate = new DateOnly(2030, 1, 6), Value = 12m, Unit = "g/dL" },
            new LaboratoryResult { VisitId = v1.Id, PatientId = s.PatientA.Id, TestName = "CBC", TestDate = new DateOnly(2030, 1, 8), Value = 13m, Unit = "g/dL" }
        ]);

        s.ActAsDoctor(s.DoctorA);
        var visit = (await s.HistoryAsync(s.PatientA)).Items.Single(item => item.VisitId == v1.Id);

        Assert.Equal(["Asthma", "Cold", "Flu"], visit.Diagnoses.Select(d => d.Name).ToArray());
        Assert.Equal(["Amoxicillin", "Zinc"], visit.Medications.Select(m => m.Name).ToArray());
        Assert.Equal(["newer", "older"], visit.Notes.Select(n => n.Content).ToArray());
        Assert.Equal(["CBC", "CBC", "Urinalysis"], visit.LabResults.Select(l => l.TestName).ToArray());
        Assert.Equal(13m, visit.LabResults[0].Value);
        Assert.Equal(12m, visit.LabResults[1].Value);
    }
}
