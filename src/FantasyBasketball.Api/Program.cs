using FantasyBasketball.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddExternalDataHttpClients();
var app = builder.Build();

app.Run();

public partial class Program;
