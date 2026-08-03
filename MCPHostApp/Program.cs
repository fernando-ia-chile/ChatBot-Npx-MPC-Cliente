using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.AspNetCore.SignalR;
using ModelContextProtocol.Client;

var builder = WebApplication.CreateBuilder(args);

var sandboxPath = Environment.GetEnvironmentVariable("MCP_FILES_ROOT");
if (string.IsNullOrWhiteSpace(sandboxPath))
{
    var containerSandboxPath = "/workspace/test-files";
    sandboxPath = !OperatingSystem.IsWindows() && Directory.Exists(containerSandboxPath)
        ? containerSandboxPath
        : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "test-files"));
}

Directory.CreateDirectory(sandboxPath);

// Crea el cliente MCP fuera de DI para evitar problemas de ciclo de vida.
// Esto inicia el servidor MCP con npx y expone el sandbox de archivos.
var mcpClientTask = McpClient.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "npx",
        Arguments = ["-y", "@modelcontextprotocol/server-filesystem", "."],
        Name = "Files MCP Server",
        WorkingDirectory = sandboxPath
    }));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Registra el cliente de chat de Azure OpenAI como singleton.
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return new ChatClientBuilder(
        new AzureOpenAIClient(
            new Uri(configuration["AZURE_OPENAI_ENDPOINT"]!),
            new DefaultAzureCredential())
        .GetChatClient(configuration["AZURE_MODEL_DEPLOYMENT"]).AsIChatClient())
    .UseFunctionInvocation()
    .Build();
});

// Registra el cliente MCP como singleton.
var mcpClient = await mcpClientTask;
builder.Services.AddSingleton(mcpClient);

var app = builder.Build();

// Configura el pipeline HTTP.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Expone el hub de SignalR.
app.MapHub<ChatHub>("/chathub");

app.Run();
