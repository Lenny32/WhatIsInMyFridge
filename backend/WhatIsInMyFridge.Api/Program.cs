using WhatIsInMyFridge.Api.Configuration;
using WhatIsInMyFridge.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddDataInfrastructure();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services
    .AddApplicationServices()
    .AddJwtAuthentication(builder.Configuration)
    .AddConfiguredCors()
    .AddJsonOptions()
    .AddOpenApiDocumentation();

var app = builder.Build();

await app.EnsureCosmosDatabaseAsync();
app.UseDevelopmentExceptionSerialization();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapDiagnosticsEndpoints(app.Environment);
app.MapAuthEndpoints();
app.MapHouseholdEndpoints();
app.MapFoodInventoryEndpoints();
app.MapRecipeEndpoints();
app.MapGroceryEndpoints();
app.MapAdminEndpoints();

app.Run();
