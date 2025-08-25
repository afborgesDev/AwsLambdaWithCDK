FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ./CreatePerson.csproj ./CreatePerson/
RUN dotnet restore ./CreatePerson.csproj

COPY . .
RUN dotnet publish ./CreatePerson.csproj -c Release -o /out/publish --no-restore

FROM public.ecr.aws/lambda/dotnet:8 AS runtime
COPY --from=build /out/publish /var/task
CMD [ "CreatePerson::CreatePerson.Function::FunctionHandler" ]