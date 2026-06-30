var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddConnectionString("iotweltdb");

var api = builder.AddProject<Projects.IotWelt_API>("iotwelt-api")
    .WithReference(sql)
    .WithHttpEndpoint(port: 5013);

builder.AddProject<Projects.MyOit_Portal>("myoit-portal")
    .WithReference(sql)
    .WithReference(api);

builder.Build().Run();
