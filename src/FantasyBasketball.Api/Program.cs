using FantasyBasketball.Api;

var builder = WebApplication.CreateBuilder(args);
ApiHost.ConfigureServices(builder);
var app = builder.Build();

ApiHost.Configure(app);
app.Run();

public partial class Program;
