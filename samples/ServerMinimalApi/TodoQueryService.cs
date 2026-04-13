using OrangeDot.Supabase;

namespace ServerMinimalApi;

public sealed class TodoQueryService
{
    private readonly ISupabaseStatelessClientFactory _clients;

    public TodoQueryService(ISupabaseStatelessClientFactory clients)
    {
        _clients = clients;
    }

    public async Task<IReadOnlyList<IntegrationTodoRecord>> GetPublicTodosAsync()
    {
        return await GetTodosAsync(_clients.CreateAnon());
    }

    public async Task<IReadOnlyList<IntegrationTodoRecord>> GetUserTodosAsync(string accessToken)
    {
        return await GetTodosAsync(_clients.CreateForUser(accessToken));
    }

    private static async Task<IReadOnlyList<IntegrationTodoRecord>> GetTodosAsync(ISupabaseStatelessClient client)
    {
        var response = await client.Postgrest.Table<IntegrationTodoRecord>()
            .Order(todo => todo.InsertedAt, global::Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(20)
            .Get();

        return response.Models;
    }
}
