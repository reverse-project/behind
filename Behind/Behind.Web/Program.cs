var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/hello", () => "Hello, world!");

await app.RunAsync();
