using FantasyBasketball.Api;

var builder = WebApplication.CreateBuilder(args);
ApiHost.ConfigureServices(builder);
var app = builder.Build();
if (await BacktestCommand.TryRunAsync(app, args) || await ModelImportCommand.TryRunAsync(app, args))
{
    return;
}

ApiHost.Configure(app);
app.Run();

public partial class Program;
