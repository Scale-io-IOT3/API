using Scale.io_API.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configure();

var app = builder.Build();
app.Configure();
app.Run();