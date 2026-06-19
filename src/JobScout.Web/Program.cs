using JobScout.Data;
using JobScout.Ingestion;
using JobScout.Web.Components;
using JobScout.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Same data + ingestion stack as the headless worker, plus the crawl loop itself — so this one
// process serves the UI and keeps the store fresh.
builder.Services.Configure<CrawlOptions>(builder.Configuration.GetSection(CrawlOptions.SectionName));
builder.Services.AddJobScoutData(builder.Configuration);
builder.Services.AddIngestion(builder.Configuration);
builder.Services.AddHostedService<CrawlService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
