using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using user_api_minimal.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<UserDB>(opt => opt.UseInMemoryDatabase("UserDB"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
var app = builder.Build();

var users = app.MapGroup("/users");

users.MapGet("/", GetAllUsers);

/**
 * A double asterisk is added before the id parameter which is called a catch-all parameter
 * The catch-all parameter escapes the appropriate characters when the route is used to generate a URL,
 * including path separator (/) characters.
 */
users.MapGet("/{**id}", GetUser);

users.MapPost("/", CreateUser);

app.Run();

static async Task<IResult> GetAllUsers(UserDB db)
{
    return TypedResults.Ok(await db.Users.ToArrayAsync());
}

static async Task<IResult> GetUser(string id, UserDB db)
{
    return await db.Users.FindAsync(id)
        is User user
            ? TypedResults.Ok(user)
            : TypedResults.NotFound();
}

static async Task<IResult> CreateUser(User user, UserDB db)
{

    if(user.Email != null && user.Email.Length > 0)
    {
        string id = GenerateUserId(user.Email);
        user.Id = id;
    }
        
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return TypedResults.Created($"/users/{user.Id}", user);
}

static string GenerateUserId(string email)
{
    string encodedUserId = "";
    string salt = "450d0b0db2bcf4adde5032eca1a7c416e560cf44";
    var encoding = new System.Text.UTF8Encoding();
    var saltBytes = encoding.GetBytes(salt);
    var emailBytes = encoding.GetBytes(email);
    using(var hmacsha1 = new HMACSHA1(saltBytes))
    {
        var idHash = hmacsha1.ComputeHash(emailBytes);
        encodedUserId = Convert.ToBase64String(idHash);
    }

    return encodedUserId;
}
