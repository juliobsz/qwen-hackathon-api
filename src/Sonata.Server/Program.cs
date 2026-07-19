using Microsoft.EntityFrameworkCore;
using Sonata.Server.Data;
using Sonata.Server.ModelProviders;
using Sonata.Server.Repositories;
using Sonata.Server.ModelProviders.Qwen;
using Sonata.Server.Conversations;
using Sonata.Server.Memories;
using Sonata.Server.Retrieval;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOptions<QwenOptions>()
    .Bind(builder.Configuration.GetSection(QwenOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins ?? "")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IMemoryService, MemoryService>();
builder.Services.AddScoped<IMemorySelector, MemorySelector>();
builder.Services.AddHttpClient<IModelProvider, QwenModelProvider>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.Run();
