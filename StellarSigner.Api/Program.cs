using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StellarSigner.Application.Abstractions.Blockchain;
using StellarSigner.Application.Abstractions.Cryptography;
using StellarSigner.Application.Abstractions.Persistence;
using StellarSigner.Application.Abstractions.Security;
using StellarSigner.Application.Common;
using StellarSigner.Application.Signatures;
using StellarSigner.Application.Signatures.Commands.SignTransaction;
using StellarSigner.Application.Signatures.Queries.GetSigningRequest;
using StellarSigner.Application.Wallets.Commands.DerivePartnerWallet;
using StellarSigner.Application.Wallets.Queries.GetPartnerWallet;
using StellarSigner.Infrastructure.DrivenAdapter.Cryptography;
using StellarSigner.Infrastructure.DrivenAdapter.MasterKey;
using StellarSigner.Infrastructure.DrivenAdapter.Persistence;
using StellarSigner.Infrastructure.DrivenAdapter.Persistence.Repositories;
using StellarSigner.Infrastructure.DrivenAdapter.SigningPolicies;
using StellarSigner.Infrastructure.DrivenAdapter.Stellar;

var builder = WebApplication.CreateBuilder(args);
var dbConnection = builder.Configuration.GetConnectionString("SignerDb") ?? throw new InvalidOperationException("ConnectionStrings:SignerDb is required");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required");
var signingKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required");
var scope = builder.Configuration["Jwt:RequiredScope"] ?? "stellar-signer.execute";
var allowedClientId = builder.Configuration["Jwt:AllowedClientId"] ?? "remittances-ms";
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || signingKey.Trim().Length < 32
    || string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(allowedClientId))
    throw new InvalidOperationException("Valid JWT issuer, audience, signing key, client identity and scope are required");
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SIGNER_WRAP_KEY")) ||
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SIGNER_MASTER_KEY_FILE")))
    throw new InvalidOperationException("Master key configuration is required");
var config = builder.Configuration.GetSection("Stellar").Get<SigningConfiguration>() ?? new SigningConfiguration();
var addressValidator = new StellarAddressValidator();
if (!addressValidator.IsValidAccount(config.Issuer) || !addressValidator.IsValidContract(config.ContractId) ||
    config.NetworkPassphrase != SigningConfiguration.TestnetPassphrase || config.MaxAmount <= 0 ||
    config.MaxOperations != 1 || config.MaxRemainingSeconds <= 0)
    throw new InvalidOperationException("Valid Stellar Testnet issuer, contract and signing limits are required");
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 262144);
builder.Services.AddSingleton(config);
builder.Services.AddDbContext<SignerDbContext>(o => o.UseNpgsql(dbConnection));
builder.Services.AddScoped<IUnitOfWork>(s => s.GetRequiredService<SignerDbContext>());
builder.Services.AddScoped<IPartnerWalletRepository, PartnerWalletRepository>();
builder.Services.AddScoped<ISigningRequestRepository, SigningRequestRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
builder.Services.AddSingleton<IMasterKeyProvider, EncryptedMasterKeyProvider>();
builder.Services.AddScoped<IKeyDerivationService, Slip10KeyDerivationService>();
builder.Services.AddSingleton<ITransactionSigner, Ed25519TransactionSigner>();
builder.Services.AddSingleton<ITransactionDecoder, StellarTransactionDecoder>();
builder.Services.AddSingleton<IStellarAddressValidator>(addressValidator);
builder.Services.AddSingleton<ISigningPolicy, AllowedAssetPolicy>();
builder.Services.AddSingleton<ISigningPolicy, AllowedDestinationPolicy>();
builder.Services.AddSingleton<ISigningPolicy, NetworkPassphrasePolicy>();
builder.Services.AddSingleton<ISigningPolicy, TransactionExpirationPolicy>();
builder.Services.AddSingleton<ISigningPolicy, TransactionLimitPolicy>();
builder.Services.AddSingleton<ISigningPolicy, AllowedContractPolicy>();
builder.Services.AddSingleton<ISigningPolicy, AllowedFunctionPolicy>();
builder.Services.AddSingleton<ISigningPolicy, SourceAccountPolicy>();
builder.Services.AddSingleton<ISigningPolicy, OperationCountPolicy>();
builder.Services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(DerivePartnerWalletCommand).Assembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient<IValidator<DerivePartnerWalletCommand>, DerivePartnerWalletValidator>();
builder.Services.AddTransient<IValidator<GetPartnerWalletQuery>, GetPartnerWalletValidator>();
builder.Services.AddTransient<IValidator<SignTransactionCommand>, SignTransactionValidator>();
builder.Services.AddTransient<IValidator<GetSigningRequestQuery>, GetSigningRequestValidator>();
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
if (builder.Environment.IsDevelopment()) builder.Services.AddOpenApi();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30)
    };
    o.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 401, Title = "UNAUTHENTICATED" });
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 403, Title = "FORBIDDEN" });
        }
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy("StellarSigner.Execute", p => p.RequireAuthenticatedUser()
    .RequireAssertion(c => c.User.FindAll("scope").Concat(c.User.FindAll("scp"))
        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Contains(scope)
        && c.User.FindFirstValue("client_id") == allowedClientId)));
var permitLimit = builder.Configuration.GetValue("RateLimit:PermitLimit", 30);
if (permitLimit <= 0) throw new InvalidOperationException("Rate limit must be positive");
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("internal", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue("client_id") ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.RejectionStatusCode = 429;
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = 429, Title = "RATE_LIMIT_EXCEEDED" }, ct);
    };
});

var app = builder.Build();
_ = app.Services.GetRequiredService<ISecretProtector>();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = ex is SignerException signer ? signer.Status : ex is DbUpdateException ? 409 : 500;
    var code = ex is SignerException se ? se.Code : ex is DbUpdateException ? "SIGNING_REQUEST_CONFLICT" : "INTERNAL_ERROR";
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = code, Type = $"https://httpstatuses.com/{status}" });
}));
app.Use(async (context, next) =>
{
    var incoming = context.Request.Headers["X-Correlation-ID"].ToString();
    context.Response.Headers["X-Correlation-ID"] = incoming.Length is > 0 and <= 64 && incoming.All(c => char.IsLetterOrDigit(c) || c == '-')
        ? incoming : Guid.NewGuid().ToString("N");
    await next(context);
});
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else
{
    app.UseHttpsRedirection();
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps) { context.Response.StatusCode = 400; return; }
        await next(context);
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (SignerDbContext db, IMasterKeyProvider master, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) && master.IsAvailable()
        ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503)).AllowAnonymous();
app.MapControllers().RequireRateLimiting("internal");
app.Run();

public partial class Program;
