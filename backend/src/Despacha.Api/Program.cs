using Despacha.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddDespacha();

var app = builder.Build();
app.UseDespacha();
app.Run();

public partial class Program;
