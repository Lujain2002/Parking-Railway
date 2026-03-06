FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# ‰”Œ „·› «·„‘—Ê⁄ Ê ÕœÌœÂ »«·«”„ ’—«Õ… ·÷„«‰ «·‹ Restore
COPY ["ParkingSystem.Relay.csproj", "./"]
RUN dotnet restore "ParkingSystem.Relay.csproj"

# ‰”Œ ﬂ· «·„·›«  Ê»‰«¡ «·„‘—Ê⁄
COPY . .
RUN dotnet publish "ParkingSystem.Relay.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ParkingSystem.Relay.dll"]