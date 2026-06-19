using JobScout.Data;
using JobScout.Ingestion;
using JobScout.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CrawlOptions>(builder.Configuration.GetSection(CrawlOptions.SectionName));

// Data layer (DbContext + repository + scorer); provider chosen from configuration.
builder.Services.AddJobScoutData(builder.Configuration);

// ATS clients (Greenhouse / Lever / Ashby) as polite typed HttpClients.
builder.Services.AddIngestion(builder.Configuration);

builder.Services.AddHostedService<CrawlService>();

var host = builder.Build();
host.Run();
