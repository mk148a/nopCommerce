using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nop.Core.Configuration;
using Nop.Web.Framework.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider((context, options) =>
{
    //we don't validate the scopes, since at the app start and the initial configuration we need 
    //to resolve some services (registered as "scoped") through the root container
    options.ValidateScopes = false;
    options.ValidateOnBuild = true;
});
builder.Configuration.AddJsonFile(NopConfigurationDefaults.AppSettingsFilePath, true, true);
builder.Configuration.AddEnvironmentVariables();

//Add services to the application and configure service provider
builder.Services.ConfigureApplicationServices(builder);

//build app
var app = builder.Build();

//Configure the application HTTP request pipeline
app.ConfigureRequestPipeline();
app.StartEngine();

//run app
app.Run();
