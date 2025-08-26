FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY lambdas/ ./lambdas/

RUN dotnet restore ./lambdas/CreatePerson/src/CreatePerson.csproj
RUN dotnet publish ./lambdas/CreatePerson/src/CreatePerson.csproj -c Release -o /out/publish --no-restore -p:PublishReadyToRun=false

FROM public.ecr.aws/lambda/dotnet:8 AS runtime
COPY --from=build /out/publish /var/task
CMD ["CreatePerson::CreatePerson.Function::FunctionHandler"]