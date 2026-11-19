using Medilog.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Medilog.WebAPI.Controllers;

[ApiController]
[Route("api/testimonials")]
[AllowAnonymous]
public sealed class TestimonialsController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;

    public TestimonialsController(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TestimonialResponse>>> GetAsync(
        CancellationToken cancellationToken)
    {
        var testimonials = await _dbContext.Testimonials
            .AsNoTracking()
            .Where(testimonial => testimonial.IsApproved)
            .OrderBy(testimonial => testimonial.DisplayOrder)
            .ThenBy(testimonial => testimonial.Id)
            .Select(testimonial => new TestimonialResponse(
                testimonial.Id,
                testimonial.QuoteAr,
                testimonial.QuoteEn,
                testimonial.NameAr,
                testimonial.NameEn,
                testimonial.RoleAr,
                testimonial.RoleEn))
            .Take(6)
            .ToListAsync(cancellationToken);

        return Ok(testimonials);
    }
}

public sealed record TestimonialResponse(
    Guid Id,
    string QuoteAr,
    string QuoteEn,
    string NameAr,
    string NameEn,
    string RoleAr,
    string RoleEn);
