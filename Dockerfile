# Acesse https://aka.ms/customizecontainer para saber como personalizar seu contêiner de depuração e como o Visual Studio usa este Dockerfile para criar suas imagens para uma depuração mais rápida.

# Esses ARGs permitem a troca da base usada para criar a imagem final durante a depuração do VS
ARG LAUNCHING_FROM_VS
# Isso define a imagem base definitiva, mas somente se LAUNCHING_FROM_VS tiver sido definido
ARG FINAL_BASE_IMAGE=${LAUNCHING_FROM_VS:+aotdebug}

# Esta fase é usada durante a execução no VS no modo rápido (Padrão para a configuração de Depuração)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Esta fase é usada para compilar o projeto de serviço
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Instalar dependências clang/zlib1g-dev para publicação no nativo
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    clang zlib1g-dev
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["rinha-back-end-2025.csproj", "."]
RUN dotnet restore "./rinha-back-end-2025.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./rinha-back-end-2025.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Esta fase é usada para publicar o projeto de serviço a ser copiado para a fase final
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN rm -rf /src/appsettings.json

RUN dotnet publish "./rinha-back-end-2025.csproj" -c $BUILD_CONFIGURATION -o /app/publish -r linux-x64 --self-contained -p:PublishAot=true -p:PublishReadyToRun=true -p:StripSymbols=true


# Esta fase é usada como base para a fase final ao iniciar no VS para dar suporte à depuração no modo normal (Padrão ao não usar a configuração de Depuração)
FROM base AS aotdebug
USER root
# Instalar o GDB para dar suporte à depuração nativa
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    gdb
USER app

# Esta fase é usada na produção ou quando executada no VS no modo normal (padrão quando não está usando a configuração de Depuração)
FROM ${FINAL_BASE_IMAGE:-mcr.microsoft.com/dotnet/runtime-deps:9.0} AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .
ENTRYPOINT ["./rinha-back-end-2025"]