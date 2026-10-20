var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Middleware: Exception Handling
app.Use(ExceptionHandlingMiddleware);

// Middleware: Authentication
app.Use(TokenAuthenticationMiddleware);

// Middleware: Request/Response Logging
app.Use(RequestResponseLoggingMiddleware);

var users = new List<User>();
var userLookup = new Dictionary<int, User>();

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/users", () => Results.Ok(users));

app.MapGet("/api/users/{id}", (int id) =>
{
    if (id <= 0) return Results.BadRequest("Invalid user ID.");
    return userLookup.TryGetValue(id, out var user) ? Results.Ok(user) : Results.NotFound();
});

app.MapPost("/api/users", (User user) =>
{
    if (string.IsNullOrWhiteSpace(user.Name)) return Results.BadRequest("Name is required.");
    if (!IsValidEmail(user.Email)) return Results.BadRequest("Invalid email format.");

    user.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
    users.Add(user);
    userLookup[user.Id] = user;
    return Results.Created($"/api/users/{user.Id}", user);
});

app.MapPut("/api/users/{id}", (int id, User updatedUser) =>
{
    if (!userLookup.TryGetValue(id, out var user)) return Results.NotFound();
    user.Name = updatedUser.Name;
    user.Email = updatedUser.Email;
    return Results.NoContent();
});

app.MapDelete("/api/users/{id}", (int id) =>
{
    if (!userLookup.TryGetValue(id, out var user)) return Results.NotFound();
    users.Remove(user);
    userLookup.Remove(id);
    return Results.NoContent();
});

app.Run();

// Middleware implementations
async Task ExceptionHandlingMiddleware(HttpContext context, Func<Task> next)
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var error = new { message = ex.Message, detail = ex.StackTrace };
        await context.Response.WriteAsJsonAsync(error);
    }
}

async Task TokenAuthenticationMiddleware(HttpContext context, Func<Task> next)
{
    var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
    if (string.IsNullOrEmpty(token) || !IsTokenValid(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Unauthorized" });
        return;
    }
    await next();
}

async Task RequestResponseLoggingMiddleware(HttpContext context, Func<Task> next)
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");

    var originalBody = context.Response.Body;
    using var memStream = new MemoryStream();
    context.Response.Body = memStream;

    await next();

    memStream.Seek(0, SeekOrigin.Begin);
    var responseText = await new StreamReader(memStream).ReadToEndAsync();
    memStream.Seek(0, SeekOrigin.Begin);

    Console.WriteLine($"Response: {context.Response.StatusCode} {responseText}");

    await memStream.CopyToAsync(originalBody);
    context.Response.Body = originalBody;
}

// Helper methods
bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch
    {
        return false;
    }
}

bool IsTokenValid(string token)
{
    return token == "valid-token";
}

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}
