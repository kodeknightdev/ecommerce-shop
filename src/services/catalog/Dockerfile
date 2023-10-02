# build context is the repo root
FROM mcr.microsoft.com/dotnet/sdk:7.0.401 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY shared/protos/ shared/protos/
COPY shared/EcommerceShop.Contracts/EcommerceShop.Contracts.csproj shared/EcommerceShop.Contracts/
COPY shared/EcommerceShop.BuildingBlocks/EcommerceShop.BuildingBlocks.csproj shared/EcommerceShop.BuildingBlocks/
COPY src/services/catalog/EcommerceShop.Catalog/EcommerceShop.Catalog.csproj src/services/catalog/EcommerceShop.Catalog/
RUN dotnet restore src/services/catalog/EcommerceShop.Catalog/EcommerceShop.Catalog.csproj

COPY shared/ shared/
COPY src/services/catalog/EcommerceShop.Catalog/ src/services/catalog/EcommerceShop.Catalog/
RUN dotnet publish src/services/catalog/EcommerceShop.Catalog/EcommerceShop.Catalog.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:7.0.11 AS runtime
RUN useradd --uid 10001 --no-create-home --shell /usr/sbin/nologin ecommerceshop
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
EXPOSE 8081
USER ecommerceshop
# no curl in the aspnet image, bash /dev/tcp does the job
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=5 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && head -1 <&3 | grep -q " 200 "'
ENTRYPOINT ["dotnet", "EcommerceShop.Catalog.dll"]
