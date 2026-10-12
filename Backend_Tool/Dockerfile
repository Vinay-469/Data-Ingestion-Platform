FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["Backend_Tool/Backend_Tool.csproj", "Backend_Tool/"]
COPY ["ApplicationLayer/ApplicationLayer.csproj", "ApplicationLayer/"]
COPY ["BusinessLayer/BusinessLayer.csproj", "BusinessLayer/"]
COPY ["CoreModels/CoreModels.csproj", "CoreModels/"]
COPY ["DataAccessLayer/DataAccessLayer.csproj", "DataAccessLayer/"]

RUN dotnet restore "Backend_Tool/Backend_Tool.csproj"

COPY . .

WORKDIR "/src/Backend_Tool"

RUN dotnet publish "Backend_Tool.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "Backend_Tool.dll"]