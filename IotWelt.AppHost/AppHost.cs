var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddConnectionString("iotweltdb");

// Schlüssel für die JWT-Signatur (Base64, mind. 32 Bytes). Wert lokal aus den User-Secrets des AppHost
// (Parameters:jwt-signing-key); fehlt er, fragt das Aspire-Dashboard danach.
var jwtSigningKey = builder.AddParameter("jwt-signing-key", secret: true);

// Mailpit fängt alle Mails der API ab (SMTP 1025), Web-UI über den Link im Aspire-Dashboard
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.31")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");
var smtp = mailpit.GetEndpoint("smtp");

var api = builder.AddProject<Projects.IotWelt_API>("iotwelt-api")
    .WithReference(sql)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithEnvironment("Smtp__Host", ReferenceExpression.Create($"{smtp.Property(EndpointProperty.Host)}"))
    .WithEnvironment("Smtp__Port", ReferenceExpression.Create($"{smtp.Property(EndpointProperty.Port)}"))
    .WithEnvironment("Smtp__Security", "None")
    .WaitFor(mailpit)
    .WithHttpEndpoint(port: 5013);

// Das Portal spricht nur mit der API, nicht mit der Datenbank
builder.AddProject<Projects.MyOit_Portal>("myoit-portal")
    .WithReference(api);

builder.Build().Run();
