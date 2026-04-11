using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace OrangeDot.Supabase.IntegrationTests;

[Table("research_organizations")]
public sealed class ResearchOrganizationTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}

[Table("research_memberships")]
public sealed class ResearchMembershipTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("organization_id")]
    public string? OrganizationId { get; set; }

    [Column("user_id")]
    public string? UserId { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }
}

[Table("research_projects")]
public sealed class ResearchProjectTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("organization_id")]
    public string? OrganizationId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("visibility")]
    public string? Visibility { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}

[Table("research_experiments")]
public sealed class ResearchExperimentTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("project_id")]
    public string? ProjectId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("baseline_run_id")]
    public string? BaselineRunId { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}

[Table("research_runs")]
public sealed class ResearchRunTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("experiment_id")]
    public string? ExperimentId { get; set; }

    [Column("display_name")]
    public string? DisplayName { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

[Table("research_run_metrics")]
public sealed class ResearchRunMetricTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("metric_name")]
    public string? MetricName { get; set; }

    [Column("metric_value")]
    public double MetricValue { get; set; }

    [Column("metric_unit")]
    public string? MetricUnit { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }
}

[Table("research_run_artifacts")]
public sealed class ResearchRunArtifactTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("run_id")]
    public string? RunId { get; set; }

    [Column("storage_bucket")]
    public string? StorageBucket { get; set; }

    [Column("object_path")]
    public string? ObjectPath { get; set; }

    [Column("file_name")]
    public string? FileName { get; set; }

    [Column("kind")]
    public string? Kind { get; set; }

    [Column("content_type")]
    public string? ContentType { get; set; }

    [Column("uploaded_by")]
    public string? UploadedBy { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}

[Table("research_decisions")]
public sealed class ResearchDecisionTestRecord : BaseModel
{
    [PrimaryKey("id", false)]
    public string? Id { get; set; }

    [Column("project_id")]
    public string? ProjectId { get; set; }

    [Column("experiment_id")]
    public string? ExperimentId { get; set; }

    [Column("baseline_run_id")]
    public string? BaselineRunId { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("summary")]
    public string? Summary { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("inserted_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime InsertedAt { get; set; }

    [Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime UpdatedAt { get; set; }
}
