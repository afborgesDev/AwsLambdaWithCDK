FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./ListPersons.csproj ./ListPersons/
RUN dotnet restore ./ListPersons.csproj
COPY . .
RUN dotnet publish ./ListPersons.csproj -c Release -o /out/publish --no-restore

FROM public.ecr.aws/lambda/dotnet:8 AS runtime
COPY --from=build /out/publish /var/task
CMD [ "ListPersons::ListPersons.Function::FunctionHandler" ]