using Microsoft.AspNetCore.Authentication.Cookies;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
var app = builder.Build();

builder.Configuration.AddJsonFile("DatabaseConfiguration.json").AddJsonFile("AdminAccessConfiguration.json");

app.UseMiddleware<CustomerPageMiddleware>();
app.UseMiddleware<AuthenticationPageMiddleware>();
app.UseMiddleware<AdminPageMiddleware>();

app.Run();
