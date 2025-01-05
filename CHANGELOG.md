# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- [Trelnex.Core.Data] - add batch transactions.

## [Trelnex.Core.Api:4.0.0] = 2025-01-04

### Added

- Added `SqlCommandProviderExtension` `AddSqlCommandProviders` `IServiceCollection` extension method

### Changed

- Consistent naming of Cosmos (tenantId, endpointUri, databaseId, containerId) and SQL (dataSource, initialCatalog, tableName) configuration.

## [Trelnex.Core.Data:4.0.0] = 2025-01-04

### Added

- Added `IQueryResult<TInterface>` as result type from `QueryCommand` `ToAsyncEnumerable`.
- `IQueryResult<TInterface>` exposes `Delete()` method to create an `ISaveCommand<TInterface>` to delete the item.
- `IQueryResult<TInterface>` exposes `Update()` method to create an `ISaveCommand<TInterface>` to update the item.

### Changed

- `QueryCommand` `ToAsyncEnumerable` returns an `IAsyncEnumerable` of `IQueryResult<TInterface>`.

## [Trelnex.Core.Data:3.1.0] = 2025-01-03

### Added

- Added `SqlCommandProvider` implementation of `ICommandProvider<TItem>` that uses a SQL table as a backing store.

## [Trelnex.Core.Api:3.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x
- Changed `CosmosCommandProviderExtensions` `AddCosmosCommandProviders` `IServiceCollection` extension method namespace from `Trelnex.Core.Api.Cosmos` to `Trelnex.Core.Api.CommandProviders`.
- Changed `CosmosCommandProviderExtensions` `AddCosmosCommandProviders` `IServiceCollection` extension method to use `CosmosCommandProviderFactory`.

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
