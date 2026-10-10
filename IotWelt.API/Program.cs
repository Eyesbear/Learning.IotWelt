using IotWelt.API.Data;
using IotWelt.API.Models;
using IotWelt.API.OpenApi;
using IotWelt.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<AppDbContext>("iotweltdb");

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

builder.Services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName);
builder.Services.AddOptions<AppLinkOptions>().BindConfiguration(AppLinkOptions.SectionName);

// ASP.NET Core Identity ohne UI und ohne Cookies — die API gibt nur Tokens aus
builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// JWT-Validierung gegen den eigenen Signierschlüssel (HS256).
// Konfiguration über Options statt direkt hier, damit Tests den Schlüssel per UseSetting setzen können.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;   // Claim-Namen wie ausgestellt behalten ("sub", "role", …)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(jwt.GetKeyBytes()),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options => options.AddAccountPolicies());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentAccount>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddTransient<IEmailSender<AppUser>, LoggingEmailSender>();
builder.Services.AddTransient<IInvitationEmailSender, LoggingEmailSender>();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.AddPreferredSecuritySchemes(BearerSecuritySchemeTransformer.SchemeName));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.SeedAdminAsync();

app.Run();
