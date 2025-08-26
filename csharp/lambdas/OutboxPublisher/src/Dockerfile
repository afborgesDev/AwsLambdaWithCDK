FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY lambdas/ ./lambdas/

RUN dotnet restore ./lambdas/OutboxPublisher/src/OutboxPublisherFunction.csproj
RUN dotnet publish ./lambdas/OutboxPublisher/src/OutboxPublisherFunction.csproj -c Release -o /out/publish --no-restore -p:PublishReadyToRun=false

FROM public.ecr.aws/lambda/dotnet:8 AS runtime
COPY --from=build /out/publish /var/task

CMD ["OutboxPublisherFunction::OutboxPublisherFunction.Function::FunctionHandler"]