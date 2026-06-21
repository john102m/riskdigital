using Microsoft.AspNetCore.SignalR;
using Risk.Server.Hubs;
using Risk.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSingleton<GameService>();

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
app.UseStaticFiles();

app.MapHub<GameHub>("/gamehub");

app.MapGet("/admin/reset", (GameService game, IHubContext<GameHub> hub) =>
{
    game.Reset();
    hub.Clients.All.SendAsync("GameStateUpdated", (object?)null);
    return Results.Ok("Reset");
});

app.MapGet("/admin/gameover", (GameService game, IHubContext<GameHub> hub) =>
{
    if (game.State is null) return Results.BadRequest("No game");
    game.State.Phase = Risk.Server.Models.GamePhase.GameOver;
    hub.Clients.All.SendAsync("GameStateUpdated", game.State);
    return Results.Ok($"Game over — winner: {game.State.Players[game.State.CurrentPlayerIndex].Name}");
});

app.Run();
