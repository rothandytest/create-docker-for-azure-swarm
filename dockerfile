FROM microsoft/dotnet:sdk AS build-env
WORKDIR /app
COPY app/*.csproj ./
RUN dotnet restore
COPY app ./
RUN dotnet publish -c Release -o out

FROM andrewroth/dotnetcore-with-azure-cli:runtime
WORKDIR /app
COPY --from=build-env /app/out ./
ENTRYPOINT ["dotnet", "app.dll"]