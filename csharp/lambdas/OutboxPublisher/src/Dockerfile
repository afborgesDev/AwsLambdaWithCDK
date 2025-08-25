FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./OutboxPublisher.csproj ./OutboxPublisher/
RUN dotnet restore ./OutboxPublisher.csproj
COPY . .
RUN dotnet publish ./OutboxPublisher.csproj -c Release -o /out/publish --no-restore

FROM public.ecr.aws/lambda/dotnet:8 AS runtime
COPY --from=build /out/publish /var/task
CMD [ "OutboxPublisher::OutboxPublisher.Function::FunctionHandler" ]