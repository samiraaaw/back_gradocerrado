# Dependencias del proyecto
- .NET 9.0
- OpenAI 2.4.0
- Docker Desktop
- VS Code con Dev Containers

# Instalación
1. Clonar repo
2. Abrir en Dev Container
3. `dotnet restore`

# API OPEN AI 

Crear pagina en GradoCerrado.Api 
appsettings.Development.json
y pegar esto y agregar la api

{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },

  "Qdrant": {
    "Url": "",
    "ApiKey": "",
    "CollectionName": "legal_documents",
    "VectorSize": 1536,
    "Distance": "Cosine"
  },

   "OpenAI": {
    "ApiKey": "",
    "Model": "gpt-4-turbo-preview",
    "MaxTokens": 2000,
    "Temperature": 0.7
},
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=pg-gradocerrado.postgres database.azure.com;Database=postgres;Username=adminuser;Password=TU_PASSWORD_AQUI;Port=5432;SSL Mode=Require;"
  }
  }