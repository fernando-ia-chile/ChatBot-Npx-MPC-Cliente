# AGENTS.md

.NET 8 (net8.0) ASP.NET Core MVC sample: a SignalR chat app that acts as an MCP **client** using the `ModelContextProtocol` NuGet package (preview). At startup it spawns `npx -y @modelcontextprotocol/server-filesystem .` and exposes those file tools to an Azure OpenAI chat model. Deployed to Azure App Service as a custom container with Node.js + npx.

## Commands

- Todos los mensajes de salida, logs, textos de UI y comentarios en código deben escribirse en **español**.
- Build: `dotnet build MCPHostApp/MCPHostApp.csproj` (from repo root)
- Run: `cd MCPHostApp && dotnet run` — requires npx on PATH and a writable `/workspace/test-files` dir (see gotchas)
- No tests, no lint, no CI, no solution file.

## Architecture

- `MCPHostApp/Program.cs:12` creates the MCP client **before `builder.Build()`** via `StdioClientTransport` (`npx -y @modelcontextprotocol/server-filesystem .`, `WorkingDirectory = "/workspace/test-files"`). If npx or that path is missing, startup fails.
- `MCPHostApp/ChatHub.cs` — SignalR hub at `/chathub`; per request it lists MCP tools, streams the LLM response (`IChatClient` via Azure OpenAI + `DefaultAzureCredential`), and echoes tool-call messages back into history.
- `test-files/` is the filesystem sandbox the MCP server operates on; the Dockerfile copies it to `/workspace/test-files` in the image.
- `infra/` — azd Bicep: Linux App Service (custom container, `WEBSITES_PORT=8080`), ACR, Azure AI Foundry + model deployment, managed identity with AcrPull/OpenAI roles. `azure.yaml` `postprovision` hook runs `az acr build` then `infra/update-container.bicep` to point the App Service at the built image.
- `Dockerfile` installs Node.js 20 in build + final stages — stock App Service .NET images lack npx, which is the whole point of this sample.
- Dev workflow is `.devcontainer`-based (codespaces), `workspaceFolder: /workspace`.

## Gotchas

- Local dev outside the devcontainer fails unless `/workspace/test-files` exists: `Program.cs:18` hardcodes that Linux container path (on Windows it resolves to `C:\workspace\test-files`).
- `appsettings.Development.json` has placeholders for `AZURE_OPENAI_ENDPOINT` / `AZURE_MODEL_DEPLOYMENT` — fill from `azd env get-values`. The app starts without them but chat errors at runtime; auth is `DefaultAzureCredential` (`az login` locally, managed identity on Azure).
- All AI/MCP packages are prerelease (`Azure.AI.OpenAI` 2.2.0-beta.4, `Microsoft.Extensions.AI` 9.6.0-preview, `ModelContextProtocol` 0.3.0-preview.1). Do not bump versions blindly; the MCP/Microsoft.Extensions.AI APIs churn between previews.
- Deploy is `azd provision` only (requires azd + az CLI; `azd auth login` and `az login` both needed). No GitHub Actions.
