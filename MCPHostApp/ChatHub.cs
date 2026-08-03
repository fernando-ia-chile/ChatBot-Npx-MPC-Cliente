using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

/// <summary>
/// Hub de SignalR para chat y descubrimiento de herramientas usando un cliente MCP.
/// </summary>
public class ChatHub : Hub
{
    // Cliente de chat que gestiona la conversación y la respuesta en streaming.
    private readonly IChatClient _chatClient;
    // Cliente MCP que expone las herramientas del servidor iniciado con npx.
    private readonly McpClient _mcpClient;

    public ChatHub(IChatClient chatClient, McpClient mcpClient)
    {
        _chatClient = chatClient;
        _mcpClient = mcpClient;
    }

    /// <summary>
    /// Procesa mensajes entrantes, transmite la respuesta del modelo e integra herramientas MCP.
    /// </summary>
    public async Task SendMessage(string user, string message, List<object> conversationHistory)
    {
        try
        {
            // Obtiene las herramientas disponibles desde el servidor MCP.
            IList<McpClientTool> tools = await _mcpClient.ListToolsAsync();

            List<ChatMessage> messages = new();
            foreach (var item in conversationHistory)
            {
                if (item is Dictionary<string, object> dict)
                {
                    var role = dict.GetValueOrDefault("role")?.ToString();
                    var content = dict.GetValueOrDefault("content")?.ToString();
                    if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(content))
                    {
                        messages.Add(new ChatMessage(
                            role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
                            content));
                    }
                }
            }

            if (messages.Count == 0)
            {
                messages.Add(new ChatMessage(ChatRole.System,
                    "Eres un asistente util con acceso a operaciones de archivos mediante herramientas MCP. " +
                    "El servidor de sistema de archivos tiene acceso al directorio /workspace/test-files. " +
                    "Cuando el usuario pida listar o acceder a archivos, usa las herramientas MCP disponibles como list_directory, read_file, etc. " +
                    "Si aparecen errores de permisos, guia al usuario para trabajar dentro de la estructura permitida."));
            }

            // Agrega el nuevo mensaje del usuario.
            messages.Add(new ChatMessage(ChatRole.User, message));

            // Notifica a los clientes que el asistente esta escribiendo.
            await Clients.All.SendAsync("TypingIndicator", true);

            List<ChatResponseUpdate> updates = [];

            // Transmite la respuesta del modelo pasando las herramientas MCP disponibles.
            await foreach (ChatResponseUpdate update in _chatClient
                .GetStreamingResponseAsync(messages, new() { Tools = [.. tools] }))
            {
                updates.Add(update);
                // Envia actualizaciones incrementales a la interfaz.
                await Clients.All.SendAsync("ReceiveMessageStream", update.Text ?? "");
            }

            // Apaga el indicador de escritura.
            await Clients.All.SendAsync("TypingIndicator", false);

            // Actualiza la conversacion con todos los mensajes, incluidas las llamadas a herramientas.
            messages.AddMessages(updates);
        }
        catch (Exception ex)
        {
            await Clients.All.SendAsync("TypingIndicator", false);
            await Clients.All.SendAsync("ReceiveMessage", "Sistema", $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Devuelve al cliente la lista de herramientas MCP disponibles.
    /// </summary>
    public async Task GetAvailableTools()
    {
        try
        {
            IList<McpClientTool> tools = await _mcpClient.ListToolsAsync();
            var toolNames = tools.Select(t => t.Name).ToList();
            await Clients.Caller.SendAsync("ReceiveAvailableTools", toolNames);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "Sistema", $"Error al obtener herramientas: {ex.Message}");
        }
    }
}
