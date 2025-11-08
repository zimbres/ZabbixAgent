var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSingleton<AgentService>();
builder.Services.AddSingleton<RadiusChecker>();
builder.Services.AddSingleton<TcpChecker>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
