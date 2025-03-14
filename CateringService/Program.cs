﻿using CateringService.Hubs;
using CateringService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Используем стандартное логирование (консоль)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Добавляем контроллеры с представлениями (если админка используется)
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Настраиваем именованные HttpClient‑ы для внешних API
builder.Services.AddHttpClient("ExternalApi", client =>
{
    var baseUrl = builder.Configuration["ExternalApi:BaseUrl"];
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddHttpClient("Orchestrator", client =>
{
    var baseUrl = builder.Configuration["Orchestrator:BaseUrl"];
    client.BaseAddress = new Uri(baseUrl);
});

// Регистрация сервисов приложения
builder.Services.AddSingleton<IVehicleRegistry, VehicleRegistry>();
builder.Services.AddSingleton<ICapacityService, CapacityService>();
builder.Services.AddSingleton<ICommModeService, CommModeService>();
builder.Services.AddSingleton<IMealTypeService, MealTypeService>();
builder.Services.AddScoped<IExternalApiService, ExternalApiService>();
builder.Services.AddScoped<ICateringProcessService, CateringProcessService>();
builder.Services.AddSingleton<IAdminConfigService, AdminConfigService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Регистрируем SignalR
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

// Если запрос по корневому URL, перенаправляем на "/admin"
app.MapGet("/", context =>
{
    context.Response.Redirect("/admin");
    return Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}"
);

app.MapHub<VehicleStatusHub>("/vehiclestatushub");

// Автоматическая инициализация транспортных средств при запуске
var cateringService = app.Services.GetRequiredService<ICateringProcessService>();
await cateringService.InitializeVehiclesAsync();

app.Run();
