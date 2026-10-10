using GVV_PR3;
using GVV_PR3.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json.Serialization;
using static System.Net.Mime.MediaTypeNames;
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
    User? user = await context.Users.Include(i => i.Role).FirstOrDefaultAsync(
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
app.MapGet("/api/clothes/search", async (PR3_Context context, [FromQuery] string text) =>
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return Results.BadRequest(new { Message = "Поисковый запрос не должен быть пустым" });
    }
    var clothes = await context.Clothes.Where(u => u.Name.Contains(text)).ToListAsync();
    return Results.Ok(clothes);
});
app.MapPost("/api/neworder", async (ClaimsPrincipal user, PR3_Context context, List<TovarOrder> tovarOrders) =>
{
    var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value??user.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(userIdClaim))
    {
        return Results.Unauthorized();
    }
    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
    {
        return Results.Unauthorized();
    }
    if (tovarOrders == null)
    {
        return Results.BadRequest(new { Message = "Список товаров не может быть пустым" });
    }
    Order newOrder = new Order()
    {
        CreatedAt = DateTime.Now,
        Status = 1,
        UserId = userId
    };
    context.Orders.Add(newOrder);
    await context.SaveChangesAsync();
    foreach (TovarOrder to in tovarOrders)
    {
        to.OrderId = newOrder.Id; 
        context.TovarOrders.Add(to);
    }
    await context.SaveChangesAsync(); 
    return Results.Ok(new { Message = "Заказ успешно создан", OrderId = newOrder.Id });
}).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole("user"));
app.MapPost("/api/orders/status", async (ClaimsPrincipal user, PR3_Context context, int status ) =>
{
    var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
    if (string.IsNullOrEmpty(userIdClaim))
    {
        return Results.Unauthorized();
    }
    if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
    {
        return Results.Unauthorized();
    }
    if (status > 0 && status < 4)
    {
        return Results.BadRequest(new { Message = "Статус товара не найден!" });
    }
    else
    {
        var orders = await context.Orders.Include(o => o.TovarOrders).ThenInclude(to => to.TovarId).Where(u => u.UserId == userId && u.Status == status).ToListAsync();
        var result = new List<OrderAndProducts>();
        foreach (var o in orders)
        {
            var clothesList = new List<ProductsForOrder>();
            foreach (var to in o.TovarOrders)
            {
                if (to.Tovar != null)
                {
                    ProductsForOrder productsForOrder = new ProductsForOrder(
                        to.Tovar.ClothesNavigation.Name,
                        to.Tovar.Size
                    );
                    clothesList.Add(productsForOrder);
                }
            }
            result.Add(new OrderAndProducts(o, clothesList));
        }
        return Results.Ok(result);
    }
}).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole("user"));
/*app.MapGet("/hash", (PR3_Context context) =>
   {
       foreach (var user in context.Users.ToList())
       {
           user.PasswordHash = hasher.HashPassword(user, user.PasswordHash);
       }
       
       context.SaveChanges();
       return Results.Ok(new
       {
           access_token = CreateToken(context.Users.First(i => i.Id == 1)),
           token_type = "Bearer"
       });
   }).AllowAnonymous();*/ //один раз захешировать тестовые данные и удалить

app.MapPost("/api/regestration", async (ClaimsPrincipal user, PR3_Context context, Request_newUser request) =>
{
    User? user_db = await context.Users.FirstOrDefaultAsync(U => U.Login == request.Login);
    if (user_db != null)
    {
        return Results.Conflict(new { Message = "Пользователь с таким логином уже существует" });
    }
    User create_user = new User {
        Adress = request.Adress,
        Login = request.Login,
        PasswordHash = request.Password,
        Phone = request.Phone,
        RoleId = 1
    };
    create_user.PasswordHash = hasher.HashPassword(create_user, request.Password);
    await context.Users.AddAsync(create_user);
    context.SaveChanges();
    return Results.Created($"/api/users/{create_user.Id}", create_user);
}).AllowAnonymous();
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
public record OrderAndProducts(Order Or, List<ProductsForOrder> Products);
public record ProductsForOrder(string ClotheName, int Size);
public record Request_newUser(string Adress, string Login, string Password, string Phone);