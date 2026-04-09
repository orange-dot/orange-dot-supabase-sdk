using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using OrangeDot.Supabase;
using ServerMinimalApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSupabaseServer(options =>
{
    options.Url = builder.Configuration["Supabase:Url"];
    options.AnonKey = builder.Configuration["Supabase:AnonKey"];
    options.ServiceRoleKey = builder.Configuration["Supabase:ServiceRoleKey"];
});
builder.Services.AddSingleton<TodoQueryService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    sample = "ServerMinimalApi"
}));

app.MapGet("/todos/public", async (TodoQueryService todos) =>
{
    var items = await todos.GetPublicTodosAsync();
    return Results.Ok(new
    {
        mode = "anon",
        count = items.Count,
        items
    });
});

app.MapGet("/todos/user", async (HttpRequest request, TodoQueryService todos) =>
{
    if (!TryGetBearerToken(request, out var accessToken))
    {
        return Results.Unauthorized();
    }

    var items = await todos.GetUserTodosAsync(accessToken);
    return Results.Ok(new
    {
        mode = "user",
        count = items.Count,
        items
    });
});

app.Run();

static bool TryGetBearerToken(HttpRequest request, out string accessToken)
{
    accessToken = string.Empty;

    if (!request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationValues))
    {
        return false;
    }

    var authorization = authorizationValues.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var token = authorization["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
    {
        return false;
    }

    accessToken = token;
    return true;
}
