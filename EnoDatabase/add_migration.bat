# Windows:
dotnet ef migrations add InitialMigrations --startup-project ..\EnoEngine
# Linux
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialMigrations --startup-project ../EnoEngine

