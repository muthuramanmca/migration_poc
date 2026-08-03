var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var identityDb = sql.AddDatabase("IdentityDb");
var flightInventoryDb = sql.AddDatabase("FlightInventoryDb");
var bookingDb = sql.AddDatabase("BookingDb");
var notificationDb = sql.AddDatabase("NotificationDb");

var rabbitMq = builder.AddRabbitMQ("RabbitMq");

var identityApi = builder.AddProject<Projects.Identity_Api>("identity-api")
    .WithReference(identityDb)
    .WithReference(rabbitMq)
    .WaitFor(identityDb)
    .WaitFor(rabbitMq);

var flightInventoryApi = builder.AddProject<Projects.FlightInventory_Api>("flight-inventory-api")
    .WithReference(flightInventoryDb)
    .WithReference(rabbitMq)
    .WaitFor(flightInventoryDb)
    .WaitFor(rabbitMq);

var bookingApi = builder.AddProject<Projects.Booking_Api>("booking-api")
    .WithReference(bookingDb)
    .WithReference(rabbitMq)
    .WaitFor(bookingDb)
    .WaitFor(rabbitMq);

var notificationApi = builder.AddProject<Projects.Notification_Api>("notification-api")
    .WithReference(notificationDb)
    .WithReference(rabbitMq)
    .WaitFor(notificationDb)
    .WaitFor(rabbitMq);

// Referencing each *.Api resource here (not just adding them above) is what lets YARP's
// service-discovery destination resolver in Gateway/appsettings.json ("http://identity-api" etc.)
// actually resolve at runtime -- Aspire injects the service-discovery env vars this depends on.
// Note: every service's "Identity:Authority" JWT-validation setting is still the appsettings.json
// placeholder (https://localhost:7297, matching Identity.Api's real launch port), not wired through
// Aspire service discovery. That's fine for same-machine dev (this AppHost's port-forwarding keeps
// that port stable), but won't survive Identity actually moving hosts -- real service-discovery-aware
// wiring is a follow-up, not part of this slice's 04_02 scope.
builder.AddProject<Projects.Gateway>("gateway")
    .WithReference(identityApi)
    .WithReference(flightInventoryApi)
    .WithReference(bookingApi)
    .WithReference(notificationApi)
    .WaitFor(identityApi)
    .WaitFor(flightInventoryApi)
    .WaitFor(bookingApi)
    .WaitFor(notificationApi);

builder.Build().Run();
