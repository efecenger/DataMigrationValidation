.PHONY: run rollback load-test build

run:
	dotnet run --project DataMigrationValidation.Console -- --batch-size=2 --workers=2

rollback:
	dotnet run --project DataMigrationValidation.Console -- --simulate-failure --batch-size=2 --workers=2

load-test:
	dotnet run --project DataMigrationValidation.LoadTests

build:
	dotnet build DataMigrationValidation.slnx
