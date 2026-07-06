using Risk.Server.EndPointConfig;
using Risk.Server.Hubs;
using Risk.Server.Services;

var builder = WebApplication.CreateBuilder(args);

var ringLogger = new RingBufferLogger();
builder.Logging.AddProvider(ringLogger);
builder.Services.AddSingleton(ringLogger);

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSingleton<GameManager>();
builder.Services.AddSingleton<AiService>();
builder.Services.AddSingleton<MlModels>();
builder.Services.AddSingleton<ActionLogger>();
builder.Services.AddSingleton<DiceAuditLogger>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<GameHub>("/gamehub");
app.MapManagementEndpoints();

// Load ML model if it exists
{
    var ml = app.Services.GetRequiredService<MlModels>();
    var actionLogger = app.Services.GetRequiredService<ActionLogger>();
    var modelsDir = Path.Combine(app.Environment.ContentRootPath, "Data", "models");
    var tempModelsDir = Path.Combine(Path.GetDirectoryName(actionLogger.LogDir)!, "risk-models");
    var blitzPath = File.Exists(Path.Combine(tempModelsDir, "blitz-model.zip"))
        ? Path.Combine(tempModelsDir, "blitz-model.zip")
        : Path.Combine(modelsDir, "blitz-model.zip");
    ml.Load(blitzPath);
    ml.LoadBehaviourModels(modelsDir);
    if (Directory.Exists(tempModelsDir))
        ml.LoadBehaviourModels(tempModelsDir);
}

app.MapFallbackToFile("index.html");

app.Run();
