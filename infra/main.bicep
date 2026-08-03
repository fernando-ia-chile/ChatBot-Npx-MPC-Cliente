@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string

param modelDeployment string

var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

resource appService 'Microsoft.Web/sites@2024-11-01' = {
  name: 'app-${resourceToken}'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|mcr.microsoft.com/dotnet/aspnet:8.0'
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      appSettings: [{
        name: 'ASPNETCORE_ENVIRONMENT'
        value: 'Production'
      }
      {
        name: 'WEBSITES_PORT'
        value: '8080'
      }
      {
        name: 'AZURE_OPENAI_ENDPOINT'
        value: cognitiveService.properties.endpoints['OpenAI Language Model Instance API']
      }
      {
        name: 'AZURE_MODEL_DEPLOYMENT'
        value: deployment.name
      }]
    }
    keyVaultReferenceIdentity: 'SystemAssigned'
    httpsOnly: true
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: 'plan-${resourceToken}'
  location: location
  tags: tags
  sku: { name: 'F1', tier: 'Free' }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: 'cr${resourceToken}'
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
    dataEndpointEnabled: false
    encryption: { status: 'disabled' }
    networkRuleBypassOptions: 'AzureServices'
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Disabled'
  }
}

resource cognitiveService 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: 'cog-${resourceToken}'
  location: location
  sku: {
    name: 'S0'
  }
  kind: 'AIServices'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    customSubDomainName: 'cog-${resourceToken}'
    allowProjectManagement: true
    publicNetworkAccess: 'Enabled'
  }
}

resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: cognitiveService
  name: modelDeployment
  sku: {
    name: 'GlobalStandard'
    capacity: 80
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelDeployment
      version: '2025-04-14'
    }
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

// AcrPull role
resource roleAssignmentAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, appService.id, '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') 
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
resource roleAssignmentAcrPullUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, principalId, '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  scope: containerRegistry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') 
    principalId: principalId
    principalType: 'User'
  }
}

// Cognitive Services OpenAI User role
resource roleAssignmentOpenAI 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, appService.id, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: cognitiveService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}
resource roleAssignmentOpenAIUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, principalId, '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
  scope: cognitiveService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: principalId
    principalType: 'User'
  }
}

// App outputs
output AZURE_RESOURCE_GROUP string = resourceGroup().name

output AZURE_CONTAINER_REGISTRY_NAME string = containerRegistry.name
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.properties.loginServer
output AZURE_APP_SERVICE_URL string = appService.properties.defaultHostName
output AZURE_APP_SERVICE_NAME string = appService.name
output AZURE_MODEL_DEPLOYMENT string = deployment.name
output AZURE_OPENAI_ENDPOINT string = cognitiveService.properties.endpoints['OpenAI Language Model Instance API']
