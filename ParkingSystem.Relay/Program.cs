using ParkingSystem.Relay;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<Worker>();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


app.MapGet("/", () => "Parking System Relay is Running and Connected to MQTT...");


app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Run();