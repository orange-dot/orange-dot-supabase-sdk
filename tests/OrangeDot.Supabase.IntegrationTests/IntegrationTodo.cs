using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace OrangeDot.Supabase.IntegrationTests;

[Table("integration_todos")]
public sealed class IntegrationTodo : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("details")]
    public string? Details { get; set; }

    [Column("owner_tag")]
    public string? OwnerTag { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }
}
