var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddConnectionString("iotweltdb");

// Schlüssel für die JWT-Signatur (Base64, mind. 32 Bytes). Wert lokal aus den User-Secrets des AppHost
// (Parameters:jwt-signing-key); fehlt er, fragt das Aspire-Dashboard danach.
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

var api = builder.AddProject<Projects.IotWelt_API>("iotwelt-api")
    .WithReference(sql)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithHttpEndpoint(port: 5013);

// Das Portal spricht nur mit der API, nicht mit der Datenbank
builder.AddProject<Projects.MyOit_Portal>("myoit-portal")
    .WithReference(api);

builder.Build().Run();
