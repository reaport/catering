using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CateringService.Services;
using CateringService.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// Регистрация сервисов
builder.Services.AddSingleton<IVehicleRegistry, VehicleRegistry>();
builder.Services.AddSingleton<ICapacityService, CapacityService>();
builder.Services.AddSingleton<IMealTypeService, MealTypeService>();
builder.Services.AddSingleton<ICommModeService, CommModeService>(); // Режим общения (Mock/Real)
builder.Services.AddScoped<IGroundControlClient, GroundControlClient>();
builder.Services.AddScoped<ICateringProcessService, CateringProcessService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Добавляем SignalR
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseHttpsRedirection();

// Маршрутизация: по умолчанию открывается страница админки
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

// Маппинг хаба
app.MapHub<VehicleStatusHub>("/vehiclestatushub");

app.Run();
