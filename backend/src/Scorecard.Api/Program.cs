using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scorecard.Api.Features.Assets;
using Scorecard.Api.Features.Calendar;
using Scorecard.Api.Features.Dashboard;
using Scorecard.Api.Features.Heatmap;
using Scorecard.Api.Features.Ingestion;
using Scorecard.Api.Features.Scores;
using Scorecard.Api.Features.TopSetups;
using Scorecard.Api.Infrastructure.Database;
using Scorecard.Api.Infrastructure.Database.ReadModel;
using Scorecard.Api.Infrastructure.Database.Seed;
using Microsoft.Extensions.Options;
using Scorecard.Api.Infrastructure.Localization;
using Scorecard.Api.Infrastructure.Providers;
using Scorecard.Api.Infrastructure.Scheduling;
using Scorecard.Api.Scoring;
using Scorecard.Api.Shared;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=scorecard;Username=scorecard;Password=scorecard";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
        // snake_case is Postgres-native and avoids quoted identifiers everywhere.
        .UseSnakeCaseNamingConvention());

// Contributors are discovered rather than listed, so adding one is a single
// file. The resolver keeps the seam for a future market that genuinely
// aggregates differently.
foreach (var type in Assembly.GetExecutingAssembly().GetTypes()
             .Where(t => t is { IsAbstract: false, IsInterface: false }
                         && typeof(IScoreContributor).IsAssignableFrom(t)))
{
    builder.Services.AddScoped(typeof(IScoreContributor), type);
}

builder.Services.AddScoped<IScoringStrategy, MacroScoringStrategy>();
builder.Services.AddScoped<IScoringStrategyResolver, ScoringStrategyResolver>();
builder.Services.AddScoped<ScoringDataLoader>();

builder.Services.AddScoped<LatestScoresQuery>();
builder.Services.AddScoped<ProvenanceQuery>();
builder.Services.AddScoped<GetTopSetupsHandler>();
builder.Services.AddScoped<GetHeatmapHandler>();
builder.Services.AddScoped<GetAssetHandler>();
builder.Services.AddScoped<ListAssetsHandler>();
builder.Services.AddScoped<GetCalendarHandler>();
builder.Services.AddScoped<GetMarketSnapshotHandler>();
builder.Services.AddScoped<CalculateScoresHandler>();
builder.Services.AddScoped<IngestReleaseHandler>();
builder.Services.AddScoped<SyncSeriesHandler>();
builder.Services.AddScoped<SyncReleasesHandler>();
builder.Services.AddScoped<SyncRunner>();
builder.Services.AddScoped<GetSyncStatusHandler>();

// The worker is only a timer; SyncRunner holds the behaviour and is also what
// POST /admin/sync/run calls, so the manual path cannot drift from the scheduled
// one. Disable via Scheduler:Enabled where FRED must not be called.
builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection(SchedulerOptions.SectionName));
builder.Services.AddHostedService<SyncWorker>();

builder.Services.Configure<FredOptions>(builder.Configuration.GetSection(FredOptions.SectionName));
builder.Services.Configure<BisOptions>(builder.Configuration.GetSection(BisOptions.SectionName));
builder.Services.Configure<EurostatOptions>(builder.Configuration.GetSection(EurostatOptions.SectionName));
builder.Services.Configure<OecdOptions>(builder.Configuration.GetSection(OecdOptions.SectionName));

builder.Services.AddHttpClient<FredClient>((sp, http) =>
{
    var fred = sp.GetRequiredService<IOptions<FredOptions>>().Value;
    http.BaseAddress = new Uri(fred.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(30);
});

// The SDMX providers are slower than FRED — the OECD in particular takes tens of
// seconds for a wide key — so they get a longer timeout rather than a retry that
// would double an already-slow call.
builder.Services.AddHttpClient<BisClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<BisOptions>>().Value.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<EurostatClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<EurostatOptions>>().Value.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<OecdClient>((sp, http) =>
{
    http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OecdOptions>>().Value.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(90);
});

// Registered by interface as well as concrete type so the registry can resolve a
// mapping row's DataSource without a switch statement anywhere in the codebase.
builder.Services.AddScoped<IReleaseProvider>(sp => sp.GetRequiredService<FredClient>());
builder.Services.AddScoped<IReleaseProvider>(sp => sp.GetRequiredService<BisClient>());
builder.Services.AddScoped<IReleaseProvider>(sp => sp.GetRequiredService<EurostatClient>());
builder.Services.AddScoped<IReleaseProvider>(sp => sp.GetRequiredService<OecdClient>());
builder.Services.AddScoped<ReleaseProviderRegistry>();
builder.Services.AddScoped<DatabaseSeeder>();

// Registered explicitly rather than pulling in the assembly-scanning package
// for a single validator.
builder.Services.AddScoped<IValidator<IngestReleaseRequest>, IngestReleaseValidator>();

builder.Services.AddScoped<LocaleContext>();
builder.Services.AddScoped<ILocaleContext>(sp => sp.GetRequiredService<LocaleContext>());

// Enums serialize as camelCase strings ("metals", "dollarIndex", "bullish") so
// they land on the frontend's existing union types with no mapping layer.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddOpenApi();

// The prototype frontend runs on 3000. The generated OpenAPI document is what
// its TypeScript types should be produced from, rather than hand-written.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:3000")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.UseMiddleware<LocaleMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", engine = EngineVersion.Current }))
    .WithTags("Ops");

app.MapFeatureEndpoints();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

app.Run();
