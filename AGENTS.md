# AGENTS.md

.NET 8 (net8.0) ASP.NET Core MVC sample: a SignalR chat app that acts as an MCP **client** using `ModelContextProtocol` 2.0.0. At startup it spawns `npx -y @modelcontextprotocol/server-filesystem .` and exposes those file tools to an Azure OpenAI chat model. Deployed to Azure App Service as a custom container with Node.js + npx.

## Commands

- Todos los mensajes de salida, logs, textos de UI y comentarios en código deben escribirse en **español**.
- Build: `dotnet build MCPHostApp/MCPHostApp.csproj` (from repo root)
- Run: `cd MCPHostApp && dotnet run` — requires `npx` on PATH. Locally it uses `../test-files`; in container it uses `/workspace/test-files`. Override with `MCP_FILES_ROOT`.
- No tests, no lint, no CI, no solution file.

## Architecture

- `MCPHostApp/Program.cs` creates the MCP client **before `builder.Build()`** via `McpClient.CreateAsync(...)` + `StdioClientTransport` (`npx -y @modelcontextprotocol/server-filesystem .`). It chooses the sandbox path in this order: `MCP_FILES_ROOT`, `/workspace/test-files` on Linux/container, else repo-local `test-files/`.
- `MCPHostApp/ChatHub.cs` — SignalR hub at `/chathub`; per request it lists MCP tools, streams the LLM response (`IChatClient` via Azure OpenAI + `DefaultAzureCredential`), and echoes tool-call messages back into history.
- `test-files/` is the filesystem sandbox the MCP server operates on; the Dockerfile copies it to `/workspace/test-files` in the image.
- `infra/` — azd Bicep: Linux App Service (custom container, `WEBSITES_PORT=8080`), ACR, Azure AI Foundry + model deployment, managed identity with AcrPull/OpenAI roles. `azure.yaml` `postprovision` hook runs `az acr build` then `infra/update-container.bicep` to point the App Service at the built image.
- `Dockerfile` installs Node.js 20 in build + final stages — stock App Service .NET images lack npx, which is the whole point of this sample.
- Dev workflow is `.devcontainer`-based (codespaces), `workspaceFolder: /workspace`.

## Gotchas

- If startup fails immediately, check `npx` first: the MCP client is created during app startup, so missing Node.js/npx prevents the web app from booting.
- `appsettings.Development.json` has placeholders for `AZURE_OPENAI_ENDPOINT` / `AZURE_MODEL_DEPLOYMENT` — fill from `azd env get-values`. The app starts without them but chat errors at runtime; auth is `DefaultAzureCredential` (`az login` locally, managed identity on Azure).
- Current direct package versions: `Azure.AI.OpenAI` 2.9.0-beta.1, `Azure.Identity` 1.21.0, `Microsoft.Extensions.AI` 10.8.3, `Microsoft.Extensions.AI.OpenAI` 10.8.3, `ModelContextProtocol` 2.0.0. `ModelContextProtocol` 2.x is a breaking API jump from the old preview line; do not downgrade/upgrade casually.
- Deploy is `azd provision` only (requires azd + az CLI; `azd auth login` and `az login` both needed). No GitHub Actions.
