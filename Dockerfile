FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TradeVault.csproj", "./"]
RUN dotnet restore "TradeVault.csproj"
COPY . .
RUN dotnet publish "TradeVault.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p Data wwwroot/uploads
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TradeVaultApp.dll"]
