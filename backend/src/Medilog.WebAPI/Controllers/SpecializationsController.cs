using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medilog.WebAPI.Controllers;

[ApiController]
[Route("api/specializations")]
[AllowAnonymous]
public sealed class SpecializationsController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<SpecializationResponse>> GetAsync()
    {
        IReadOnlyList<SpecializationResponse> specializations =
        [
            new("cardiology", "Cardiology", "أمراض القلب"),
            new("dermatology", "Dermatology", "الأمراض الجلدية"),
            new("dentistry", "Dentistry", "طب الأسنان"),
            new("general-medicine", "General Medicine", "طب عام"),
            new("neurology", "Neurology", "طب الأعصاب"),
            new("obstetrics-gynecology", "Obstetrics and Gynecology", "النساء والتوليد"),
            new("orthopedics", "Orthopedics", "جراحة العظام"),
            new("pediatrics", "Pediatrics", "طب الأطفال"),
            new("psychiatry", "Psychiatry", "الطب النفسي"),
            new("radiology", "Radiology", "الأشعة"),
            new("surgery", "Surgery", "الجراحة"),
            new("ophthalmology", "Ophthalmology", "طب العيون")
        ];

        return Ok(specializations);
    }
}

public sealed record SpecializationResponse(string Key, string NameEn, string NameAr);
