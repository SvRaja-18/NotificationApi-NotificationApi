# Build and publish stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first to leverage Docker layer caching
COPY ["src/NotificationApi/NotificationApi.csproj", "src/NotificationApi/"]
RUN dotnet restore "src/NotificationApi/NotificationApi.csproj"

# Copy source code and publish release build
COPY src/ src/
WORKDIR "/src/src/NotificationApi"
RUN dotnet publish "NotificationApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Configure default ports and bindings
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Dynamically listen on $PORT if provided by host environment (e.g., Render, Cloud Run, Railway)
# or default to 8080
ENTRYPOINT ["sh", "-c", "exec dotnet NotificationApi.dll --urls http://0.0.0.0:${PORT:-8080}"]
