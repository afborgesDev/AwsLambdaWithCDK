docker build -f lambdas/CreatePerson/src/Dockerfile .
docker build -f lambdas/OutboxPublisher/src/Dockerfile .
docker build -f lambdas/ListPersons/src/Dockerfile .