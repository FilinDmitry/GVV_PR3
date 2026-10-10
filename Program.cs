using Azure.Core;
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

builder.Services.AddDbContext<PR3_Context>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

app.MapGet("/api/clothes", async (PR3_Context context, [FromQuery] string? text) =>
{
    var query = context.Clothes.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(text))
    {
        query = query.Where(c => c.Name.Contains(text));
    }

    var clothes = await query.Select(c => new LookClothe(c.Name, c.Description, c.TypeNavigation != null ? c.TypeNavigation.Name : "Без типа", c.PurchaseAmount)).ToListAsync();

    return Results.Ok(clothes);
});
app.MapPost("/api/neworder", async (ClaimsPrincipal user, PR3_Context context, List<NewOrderItem> tovarOrders) =>
{
    var userIdClaim = user.FindFirst("sub")?.Value;

    if (!int.TryParse(userIdClaim, out int userId))
        return Results.Unauthorized();

    if (tovarOrders == null || tovarOrders.Count == 0)
        return Results.BadRequest(new { Message = "Список товаров не может быть пустым" });

    if (tovarOrders.Any(x => x.Quantity <= 0))
        return Results.BadRequest(new { Message = "Количество должно быть больше 0" });

    var newOrder = new Order
    {
        CreatedAt = DateTime.UtcNow,
        Status = 1,
        UserId = userId
    };

    context.Orders.Add(newOrder);

    foreach (var item in tovarOrders)
    {
        context.TovarOrders.Add(new TovarOrder
        {
            Order = newOrder,
            TovarId = item.TovarId,
            Amount = item.Quantity
        });
    }

    await context.SaveChangesAsync();

    return Results.Ok(new
    {
        Message = "Заказ успешно создан",
        OrderId = newOrder.Id
    });
}).RequireAuthorization();
app.MapGet("/api/orders/status/{status:int}", async (ClaimsPrincipal user, PR3_Context context, int status) =>
{
    if (status < 1 || status > 3)
        return Results.BadRequest(new { Message = "Статус заказа не найден!" });

    var userIdClaim = user.FindFirst("sub")?.Value;

    if (!int.TryParse(userIdClaim, out int userId))
        return Results.Unauthorized();

    var orders = await context.Orders.AsNoTracking().Include(o => o.TovarOrders).ThenInclude(to => to.Tovar).ThenInclude(t => t.ClothesNavigation).Where(o => o.UserId == userId && o.Status == status).ToListAsync();
    var result = orders.Select(o => new OrderAndProducts(
        o,
        o.TovarOrders.Where(to => to.Tovar != null && to.Tovar.ClothesNavigation != null).Select(to => new ProductsForOrder(to.Tovar.ClothesNavigation.Name, to.Tovar.Size)).ToList())).ToList();
    return Results.Ok(result);
}).RequireAuthorization();
app.MapGet("/api/orders/customer/{customerId:int}", async (ClaimsPrincipal user, PR3_Context context, int customerId) =>
{
    var userIdClaim = user.FindFirst("sub")?.Value;

    if (!int.TryParse(userIdClaim, out int currentUserId))
        return Results.Unauthorized();
    if (!user.IsInRole("manager") && currentUserId != customerId)
        return Results.Forbid();
    var orders = await context.Orders.AsNoTracking().Include(o => o.TovarOrders).ThenInclude(to => to.Tovar).ThenInclude(t => t.ClothesNavigation).Where(o => o.UserId == customerId).ToListAsync();

    var result = orders.Select(o => new OrderAndProducts(
        o,
        o.TovarOrders
            .Where(to => to.Tovar != null && to.Tovar.ClothesNavigation != null)
            .Select(to => new ProductsForOrder(
                to.Tovar.ClothesNavigation.Name,
                to.Tovar.Size))
            .ToList()
    )).ToList();

    return Results.Ok(result);
}).RequireAuthorization();
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

app.MapPost("/api/regestration", async (PR3_Context context, Request_newUser request) =>
{
    User? user_db = await context.Users.FirstOrDefaultAsync(U => U.Login == request.Login);
    if (user_db != null)
    {
        return Results.Conflict(new { Message = "Пользователь с таким логином уже существует" });
    }
    User create_user = new User
    {
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
app.MapPatch("/api/orders/{id}", async (ClaimsPrincipal user, PR3_Context context, int id, Int_request request) =>
{
    Order? order = await context.Orders.FirstOrDefaultAsync(o => o.Id == id);
    if (order == null)
    {
        return Results.NotFound();
    }
    if (request.num < 0 || request.num > 3)
    {
        return Results.BadRequest(new { Message = "Введен ID статуса за пределами списка" });
    }
    order.Status = request.num;
    await context.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole("manager"));
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
public record NewOrderItem(int TovarId, int Quantity);
public record OrderAndProducts(Order Or, List<ProductsForOrder> Products);
public record ProductsForOrder(string ClotheName, int Size);
public record LookClothe(string Name, string Description, string type, int amount);
public record Request_newUser(string Adress, string Login, string Password, string Phone);
public record Int_request(int num);