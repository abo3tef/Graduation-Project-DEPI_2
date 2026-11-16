namespace Medilog.Domain.Enums;

/// <summary>
/// The kind of account that owns a record. It is persisted on
/// <see cref="Entities.AuthToken"/> so that a token can point at the correct account table
/// (there is one account table per role and no shared users table).
/// </summary>
public enum UserType
{
    Doctor,
    Secretary,
    Admin
}
