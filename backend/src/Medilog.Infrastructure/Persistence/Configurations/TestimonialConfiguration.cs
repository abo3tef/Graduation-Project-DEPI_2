using Medilog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Medilog.Infrastructure.Persistence.Configurations;

internal sealed class TestimonialConfiguration : IEntityTypeConfiguration<Testimonial>
{
    public void Configure(EntityTypeBuilder<Testimonial> builder)
    {
        builder.ToTable("testimonials");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(entity => entity.QuoteAr).HasColumnName("quote_ar").HasMaxLength(800).IsRequired();
        builder.Property(entity => entity.QuoteEn).HasColumnName("quote_en").HasMaxLength(800).IsRequired();
        builder.Property(entity => entity.NameAr).HasColumnName("name_ar").HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.NameEn).HasColumnName("name_en").HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RoleAr).HasColumnName("role_ar").HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.RoleEn).HasColumnName("role_en").HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.IsApproved).HasColumnName("is_approved").IsRequired();
        builder.Property(entity => entity.DisplayOrder).HasColumnName("display_order").IsRequired();
        builder.HasIndex(entity => new { entity.IsApproved, entity.DisplayOrder });
    }
}
