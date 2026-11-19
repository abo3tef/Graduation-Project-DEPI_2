using Medilog.Domain.Common;

namespace Medilog.Domain.Entities;

public sealed class Testimonial : AuditableEntity
{
    public string QuoteAr { get; set; } = string.Empty;
    public string QuoteEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string RoleAr { get; set; } = string.Empty;
    public string RoleEn { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public int DisplayOrder { get; set; }
}
