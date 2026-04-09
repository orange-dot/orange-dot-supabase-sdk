using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace ServerMinimalApi;

[Table("integration_todos")]
public sealed class IntegrationTodoRecord : BaseModel
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
