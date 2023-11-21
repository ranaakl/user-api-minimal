using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using user_api_minimal.Models;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

var builder = WebApplication.CreateBuilder(args);
//In-memory User database definition
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

/**
 * A double asterisk is added before the id parameter as it might contain a slash.
 * This is called a catch-all parameter which escapes the appropriate characters
 * when the route is used to generate a URL, including path separator (/) characters.
 */
user.MapGet("/{**id}", GetUser).RequireAuthorization();

user.MapPost("/", CreateUser);

app.UseAuthentication();
app.UseAuthorization();

app.Run();

/**
 * This method retrieves a user from the database using the given id 
 */
static async Task<IResult> GetUser(string id, UserDB db)
{
    if (await db.Users.FindAsync(id) is User user)
    {
        if (!user.MarketingConsent)
        {
            // user email is set to null in case of MarketingConsent is false
            // to remove the email property.
            user.Email = null;
        }
        return TypedResults.Ok(user);
    }
    return TypedResults.NotFound();
}

/**
 * This method is used persists a user in the database
 */
async Task<IResult> CreateUser(UserDTO userDTO, UserDB db)
{
    //user to be persisted in the database
    User user = new();

    //The email has to have a value as it is used to generate the id
    if(userDTO.Email != null && userDTO.Email.Length > 0)
    {
        string id = GenerateUserId(userDTO.Email);
        //Filling up the user object from the user DTO
        user.Id = id;
        user.FirstName = userDTO.FirstName;
        user.LastName = userDTO.LastName;
        user.Email = userDTO.Email;
        user.MarketingConsent = userDTO.MarketingConsent;
    }
    
    //saving the user object to the database
    db.Users.Add(user);
    await db.SaveChangesAsync();

    //creating the JWT access token for the saved user and returning the id and accessToken.
    return createAccessToken(user, db);
}

/**
 * This method generates the user's id from the email address
 */
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

/**
 * This method creates the JWT access token if the given user exists.
 * It returns a Json object of the id and accessToken created else returns 401 unauthorized
 */
IResult createAccessToken(User user, UserDB db)
{
    string userId = isUserExisting(user.Id, db);

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
            //sets the accessToken to expire after 5 minutes
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials
            (new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var stringToken = tokenHandler.WriteToken(token);
        UserJwtAuth auth = new()
        {
            Id = user.Id,
            AccessToken = stringToken
        };

        return TypedResults.Ok(auth);
    }
    return TypedResults.Unauthorized();
}

/**
 * This method is used to check if the user with the given id exists.
 * returns the user's id if the user exists else returns null
 */
static string isUserExisting(string id, UserDB db)
{
    User user = db.Users.Find(id);

    return user != null ? user.Id : null;
}
