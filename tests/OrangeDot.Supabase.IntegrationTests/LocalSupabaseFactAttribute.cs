using Xunit;

namespace OrangeDot.Supabase.IntegrationTests;

public sealed class LocalSupabaseFactAttribute : FactAttribute
{
    public LocalSupabaseFactAttribute()
    {
        var settings = IntegrationTestEnvironment.LoadSettings();

        if (!settings.IsEnabled)
        {
            Skip = $"Local Supabase integration tests are disabled. Set {IntegrationTestEnvironment.RunIntegrationVariableName}=1, run `supabase start`, then rerun `dotnet test`.";
        }
    }
}
