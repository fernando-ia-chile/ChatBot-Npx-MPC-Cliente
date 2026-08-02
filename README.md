# Imagen Docker ASP.NET + npx con cliente MCP

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Azure-Samples/app-service-ai-dotnet-chatbot-npx-mcp-client)

Este ejemplo demuestra cómo ejecutar un cliente del Model Context Protocol (MCP) dentro de Azure App Service usando un contenedor personalizado que incluye tanto el runtime de .NET como Node.js/npx. Las imágenes Linux predeterminadas de App Service para .NET no incluyen Node.js ni npx, que es un método popular para ejecutar un servidor MCP (como [@modelcontextprotocol/server-filesystem](https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem)). Al crear una imagen Docker personalizada con ambos runtimes, este ejemplo permite que tu aplicación ASP.NET Core invoque herramientas MCP usando npx en tiempo de ejecución.

![captura de pantalla de la aplicación en ejecución](image.png)

**Cómo funciona:**

- El Dockerfile crea una imagen con .NET y Node.js (con npx).
- La aplicación usa el paquete NuGet ModelContextProtocol para crear un cliente MCP que inicia el servidor MCP usando npx (consulta `Program.cs`).
- El servidor MCP se inicia en el directorio `/workspace/test-files`, exponiendo herramientas del sistema de archivos a la aplicación.
- La aplicación ASP.NET Core registra el cliente MCP como proveedor de herramientas y expone los puntos de conexión de chat y descubrimiento de herramientas mediante SignalR (consulta `ChatHub.cs`).
- La aplicación se implementa en Azure App Service como contenedor personalizado, con identidad administrada y todas las variables de entorno requeridas configuradas mediante Bicep y azd.

Este enfoque permite que tu aplicación .NET use herramientas MCP que requieren Node.js/npx, incluso en entornos (como Azure App Service) donde las imágenes predeterminadas no las admiten de fábrica.

## Arquitectura

La plantilla AZD incluida aprovisiona los siguientes recursos de Azure:

- **Azure App Service** - Aloja la aplicación web ASP.NET Core con contenedor personalizado
- **Azure Container Registry** - Almacena la imagen del contenedor
- **Azure AI Foundry** - Proporciona modelos OpenAI GPT
- **Identidad administrada** - Para la autenticación segura entre servicios

## Implementación en Azure

1. Abre el repositorio en un codespace.

2. Inicia sesión en Azure:

```bash
azd auth login
az login
```

3. Aprovisiona los recursos:

```bash
azd provision
```

Esto:
- Aprovisionará todos los recursos de Azure
- Compilará y enviará la imagen del contenedor a Azure Container Registry
- Implementará la aplicación en Azure App Service
- Configurará la identidad administrada y las asignaciones de roles

## Desarrollo local

1. En la terminal, después de que `azd provision` finalice, obtén los valores de `AZURE_OPENAI_ENDPOINT` y `AZURE_MODEL_DEPLOYMENT`.

    ```bash
    azd env get-values
    ```

2. Abre *MCPHostApp/appsettings.Development.json* y agrega el valor de las dos variables.

3. En la terminal, ejecuta la aplicación.

    ```bash
    cd MCPHostApp
    dotnet run
    ```

4. Selecciona **Abrir en el explorador**.
