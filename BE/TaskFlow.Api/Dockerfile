FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TaskFlow.Api/TaskFlow.Api.csproj", "TaskFlow.Api/"]
COPY ["TaskFlow.BusinessLogic/TaskFlow.BusinessLogic.csproj", "TaskFlow.BusinessLogic/"]
COPY ["TaskFlow.Common/TaskFlow.Common.csproj", "TaskFlow.Common/"]
COPY ["TaskFlow.Repositories/TaskFlow.Repositories.csproj", "TaskFlow.Repositories/"]
COPY ["TaskFlow.DataAccess/TaskFlow.DataAccess.csproj", "TaskFlow.DataAccess/"]
RUN dotnet restore "./TaskFlow.Api/./TaskFlow.Api.csproj"
COPY . .
WORKDIR "/src/TaskFlow.Api"
RUN dotnet build "./TaskFlow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TaskFlow.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]