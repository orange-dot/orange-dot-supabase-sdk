using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace OrangeDot.Supabase.Unity.Samples.AuthAndData
{
[Table("unity_todos")]
public sealed class UnityTodoItem : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
}
