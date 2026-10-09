using GVV_PR3;
using GVV_PR3.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
var hasher = new PasswordHasher<User>();
var validation = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = AuthOptions.ISSUER,

    ValidateAudience = true,
    ValidAudience = AuthOptions.AUDIENCE,

    ValidateIssuerSigningKey = true,
    IssuerSigningKey = AuthOptions.GetSymmetricSecurityKey(),

    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero,

    NameClaimType = "name",
    RoleClaimType = "role"
};

builder.Services.AddDbContext<PR3_Context>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = validation;
    });

builder.Services.AddAuthorization();



var app = builder.Build();


app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth", async (PR3_Context context, AuthRequest request) =>
{
    User? user = await context.Users.FirstOrDefaultAsync(
        i => i.Login == request.Login);

    if (user == null || string.IsNullOrWhiteSpace(request.Password))
        return Results.Challenge();

    var result = hasher.VerifyHashedPassword(
        user, user.PasswordHash, request.Password);

    if (result == PasswordVerificationResult.Failed)
        return Results.Challenge();

    return Results.Ok(new
    {
        access_token = CreateToken(user),
        token_type = "Bearer"
    });
}).AllowAnonymous();

app.MapGet("/api/clothes", (PR3_Context context) => Results.Json(context.Clothes));
app.MapGet("/api/users/{text}", (PR3_Context context, string text) =>
{
    List<Clothe> clothes = context.Clothes.Where(u => u.Name.Contains(text)).ToList();
    return Results.Json(clothes);
});
app.MapPost("/api/users", async (PR3_Context context, List<Clothe> clothe) => {
    foreach (Clothe clo in clothe)
    {
        context.Clothes.Add(clothe);
        await context.SaveChangesAsync();
    }
    context.Clothes.Add(clothe);
    await context.SaveChangesAsync();
    return Results.Created($"/api/clothes/{clothe.Id}", clothe);
});
/*app.MapGet("/hash", (RC_SkladContext context) =>
   {
       foreach (var user in context.Users.ToList())
       {
           user.Password = hasher.HashPassword(user, user.Password);
       }
       
       context.SaveChanges();
       return Results.Ok(new
       {
           access_token = CreateToken(context.Users.Include(u => u.IdtypeNavigation).First(i => i.Id == 1)),
           token_type = "Bearer"
       });
   }).AllowAnonymous();*/ //один раз захешировать тестовые данные и удалить

app.Run();

string CreateToken(User user)
{

    var claims = new[]
    {

        new Claim("sub", user.Id.ToString()),
        new Claim("name", user.Login),
        new Claim("role", user.Role.Name)
    };

    var token = new JwtSecurityToken(
        issuer: AuthOptions.ISSUER,
        audience: AuthOptions.AUDIENCE,
        claims: claims,
        expires: DateTime.UtcNow.AddDays(1),
        signingCredentials: new SigningCredentials(
            AuthOptions.GetSymmetricSecurityKey(),
            SecurityAlgorithms.HmacSha256
            ));

    return new JwtSecurityTokenHandler().WriteToken(token);
}

public record AuthRequest(string Login, string Password);