FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["Micromarin.OAuth.PartnerTest.csproj", "./"]
RUN dotnet restore "Micromarin.OAuth.PartnerTest.csproj"

COPY . .
RUN dotnet publish "Micromarin.OAuth.PartnerTest.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

COPY --from=build /app/publish .

# Render provides PORT at runtime; fallback is 10000 for local container tests.
CMD ["sh", "-c", "dotnet Micromarin.OAuth.PartnerTest.dll --urls http://0.0.0.0:${PORT:-10000}"]
