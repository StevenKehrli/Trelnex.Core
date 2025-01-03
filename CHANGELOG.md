# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- [Trelnex.Core.Data:3.x.0] - add `SqlCommandProvider` that uses an Azure SQL database as backing store.
- [Trelnex.Core.Api.Tests:3.x.0] - add integration tests for `AddSqlCommandProviders` `IServiceCollection` extension method.
- [Trelnex.Core.Data:3.x.0] - add batch transactions.

## [Trelnex.Core.Api:3.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x
- Changed `CosmosExtensions` `AddCosmosCommandProviders` `IServiceCollection` extension method namespace from `Trelnex.Core.Api.Cosmos` to `Trelnex.Core.Api.CommandProviders`.
- Changed `CosmosExtensions` `AddCosmosCommandProviders` `IServiceCollection` extension method to use `CosmosCommandProviderFactory`.

## [Trelnex.Core.Data.Emulator:3.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x

## [Trelnex.Core.Data:3.0.0] - 2025-01-02

### Added

- Added `CosmosCommandProviderFactory` to create an instance of `CosmosCommandProvider`. This now handles all CosmosClient and KeyResolver logic (with the exception of the TokenCredential).

### Changed

- Updated to dotnet 9.0.x

## [Trelnex.Core.Client:2.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x

## [Trelnex.Core.HttpStatusCode:2.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x

## [Trelnex.Core.Api:2.0.0] - 2024-10-22

### Added

- Added `NoAuthentication` `IServiceCollection` extension method to register empty authentication and authorization policy services.
- Throw exception if authentication is not configured.

### Changed

- Updated to [Trelnex.Core.Data:2.0.0].

## [Trelnex.Core.Data.Emulator:2.0.0] - 2024-10-22

### Changed

- Updated to [Trelnex.Core.Data:2.0.0].

## [Trelnex.Core.Data:2.0.0] - 2024-10-22

### Removed

- Removed `HttpBearerToken` from `IRequestContext`.
