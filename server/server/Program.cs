using Microsoft.EntityFrameworkCore;
using server.Data;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();
//DB registering here

builder.Services.AddDbContext<AppDbContext>(options =>
options.UseSqlServer
(builder.Configuration.GetConnectionString
("DefaultString")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
