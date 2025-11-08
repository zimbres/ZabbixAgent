var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ZabbixAgent>("zabbixagent");

builder.Build().Run();
