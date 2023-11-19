using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using user_api_minimal.Models;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<UserDB>(opt => opt.UseInMemoryDatabase("UserDB"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true,
    };
});
builder.Services.AddAuthorization();
var app = builder.Build();

var user = app.MapGroup("/user");

user.MapGet("/", GetAllUsers);

/**
 * A double asterisk is added before the id parameter which is called a catch-all parameter
 * The catch-all parameter escapes the appropriate characters when the route is used to generate a URL,
 * including path separator (/) characters.
 */
user.MapGet("/{**id}", GetUser).RequireAuthorization();

user.MapPost("/", CreateUser);

user.MapGet("/Jwt/demo", () => "Hello World!").RequireAuthorization();

app.UseAuthentication();
app.UseAuthorization();

app.Run();

static async Task<IResult> GetAllUsers(UserDB db)
{
    return TypedResults.Ok(await db.Users.ToArrayAsync());
}

static string isUserExisting(string id, UserDB db)
{
    User user = db.Users.Find(id);

    return user != null ? user.Id : null ;
}

static async Task<IResult> GetUser(string id, UserDB db)
{
    return await db.Users.FindAsync(id)
        is User user
            ? TypedResults.Ok(user)
            : TypedResults.NotFound();
}

async Task<IResult> CreateUser(User user, UserDB db)
{

    if(user.Email != null && user.Email.Length > 0)
    {
        string id = GenerateUserId(user.Email);
        user.Id = id;
    }
        
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return createAccessToken(user, db);
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

IResult createAccessToken(User user, UserDB db)
{
    string userId = isUserExisting(user.Id, db);
    // TODO: replace with Id from DB
    if (userId != null && user.Id == userId)
    {
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                    new Claim("Id", Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Sub, user.FirstName),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                }),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials
            (new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = tokenHandler.WriteToken(token);
        var stringToken = tokenHandler.WriteToken(token);
        UserJwtAuth auth = new()
        {
            Id = user.Id,
            AccessToken = stringToken
        };

        return Results.Ok(auth);
    }
    return Results.Unauthorized();

}

