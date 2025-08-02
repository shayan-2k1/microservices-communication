using Microsoft.EntityFrameworkCore;
using OrderDomain.Interfaces;
using OrderInfrastructure.Databases;
using OrderInfrastructure.Repository;
using Grpc.Net.Client;
using Grpc.Core;
using GrpcService.Protos;
using Confluent.Kafka;
using System.Text.Json;
using Newtonsoft.Json;
using RabbitEventHandler.Services;
using KafkaEventHandler.Services;
using Shared.Mapper;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<OrderDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddTransient<IOrderService, OrderRepository>();
builder.Services.AddAutoMapper(typeof(Program).Assembly, typeof(MapperProfile).Assembly);
builder.Services.AddControllers();
builder.Services.AddGrpcClient<ProductGrpc.ProductGrpcClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcClient:Url"]!);
});

builder.Services.AddTransient<RabbitMqEventService>();

builder.Services.AddScoped<KafkaEventHandlerService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();


