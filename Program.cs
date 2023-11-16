using Microsoft.EntityFrameworkCore;
using user_api_minimal.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<UserDB>(opt => opt.UseInMemoryDatabase("UserDB"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
var app = builder.Build();

app.MapGet("/users", async (UserDB db) =>
    await db.Users.ToListAsync());

app.MapGet("/users/{id}", async (string id, UserDB db) =>
    await db.Users.FindAsync(id)
        is User user
            ? Results.Ok(user)
            : Results.NotFound());

app.MapPost("/users", async (User user, UserDB db) =>
{
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}", user);
});

app.Run();
