# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- OpenTelemetry
- Support for other credential types: AWS, GCP, Okta, etc.

### Changed

- Investigate change to `CredentialFactory` to support dependency injection and configuration through `IConfiguration`
- Upload symbols to GitHub

### Deleted

## [Trelnex.Core.Api:5.1.0] - 2025-01-10

### Changed

- Updated to [Trelnex.Core.Data:5.0.0].
- Throw an `InvalidOperationException` is an `ICommandProvider<TInterface>` is registered multiple times.

## [Trelnex.Core.Data:5.1.0] - 2025-01-10

### Added

- Added `IBatchCommand<TInterface>` to save a batch of `ISaveCommand<TInterface>`.
- Concurrency updates and fixes to `InMemoryCommandProvider`.

## [Trelnex.Core.Api:5.0.0] - 2025-01-07

### Added

- Added `InMemory.AddSqlCommandProviders()` `IServiceCollection` extension method

### Changed

- Updated to [Trelnex.Core.Data:5.0.0].

## [Trelnex.Core.Data.Emulator] - 2025-01-07

### Deleted

- Moved the `InMemoryCommandProvider` into `Trelnex.Core.Data`.
- `[Trelnex.Core.Data.Emulator]` is now deleted.

## [Trelnex.Core.Data:5.0.0] - 2025-01-07

### Added

- Moved the `InMemoryCommandProvider` into `Trelnex.Core.Data`.

## [Trelnex.Core.Api:4.1.2] - 2025-01-07

### Changed

- Updated to [Trelnex.Core.Data:4.1.2].

## [Trelnex.Core.Emulator:4.1.2] - 2025-01-07

### Changed

- Updated to [Trelnex.Core.Data:4.1.2].

## [Trelnex.Core.Data:4.1.2] - 2025-01-07

### Changes

- Moved the exclusive lock in `ISaveCommand<TInterface>.SaveAsync()` to its `ProxyManager` base class.
- Change `IQueryResult<TInterface>.Delete()` and `IQueryResult<TInterface>.Update()` methods to use the `ProxyManager` base class exclusive lock.

## [Trelnex.Core.Emulator:4.1.1] - 2025-01-07

### Changed

- Updated to [Trelnex.Core.Data:4.1.1].

## [Trelnex.Core.Data:4.1.1] - 2025-01-07

### Added

- Add an exclusive lock to `ISaveCommand<TInterface>.SaveAsync()` method to ensure that only one save operation is in progress at a time.
- Add checks to `IQueryResult<TInterface>.Delete()` and `IQueryResult<TInterface>.Update()` methods to ensure that only one `ISaveCommand<TInterface>` can be created.

## [Trelnex.Core.Api:4.1.0] - 2025-01-05

### Added

- Register `CosmosCommandProviderHealthCheck` as health check for the underlying CosmosDB.
- Register `SqlCommandProviderHealthCheck` as health check for the underlying SQL connection.

### Changed

- Updated to [Trelnex.Core.Data:4.1.0].

## [Trelnex.Core.Data.Emulator:4.1.0] - 2025-01-05

### Changed

- Updated to [Trelnex.Core.Data:4.1.0].
- Bug fixes to `InMemoryCommandProvider` for consistency with other command providers.

## [Trelnex.Core.Data:4.1.0] - 2025-01-05

### Added

- Added `CosmosCommandProviderFactory.GetStatus()` method as health check for the underlying CosmosDB.
- Added `SqlCommandProviderFactory.GetStatus()` method as health check for the underlying SQL connection.

## [Trelnex.Core.Api:4.0.0] - 2025-01-04

### Added

- Added `SqlCommandProviderExtensions.AddSqlCommandProviders()` `IServiceCollection` extension method

### Changed

- Updated to [Trelnex.Core.Data:4.0.0].
- Consistent naming of Cosmos (tenantId, endpointUri, databaseId, containerId) and SQL (dataSource, initialCatalog, tableName) configuration.

## [Trelnex.Core.Data:4.0.0] - 2025-01-04

### Added

- Added `IQueryResult<TInterface>` as result type from `IQueryCommand<TInterface>.ToAsyncEnumerable()`.
- `IQueryResult<TInterface>` exposes `Delete()` method to create an `ISaveCommand<TInterface>` to delete the item.
- `IQueryResult<TInterface>` exposes `Update()` method to create an `ISaveCommand<TInterface>` to update the item.

### Changed

- `QueryCommand` `ToAsyncEnumerable` returns an `IAsyncEnumerable` of `IQueryResult<TInterface>`.

## [Trelnex.Core.Api:3.1.0] - 2025-01-03

### Changed

- Updated to [Trelnex.Core.Data:3.1.0].

## [Trelnex.Core.Data.Emulator:3.1.0] - 2025-01-03

### Changed

- Updated to [Trelnex.Core.Data:3.1.0].

## [Trelnex.Core.Data:3.1.0] - 2025-01-03

### Added

- Added `SqlCommandProvider` implementation of `ICommandProvider<TItem>` that uses a SQL table as a backing store.

## [Trelnex.Core.Api:3.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x
- Updated to [Trelnex.Core.Data:3.0.0].
- Changed `CosmosCommandProviderExtensions.AddCosmosCommandProviders()` `IServiceCollection` extension method namespace from `Trelnex.Core.Api.Cosmos` to `Trelnex.Core.Api.CommandProviders`.
- Changed `CosmosCommandProviderExtensions.AddCosmosCommandProviders()` `IServiceCollection` extension method to use `CosmosCommandProviderFactory`.

## [Trelnex.Core.Data.Emulator:3.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x
- Updated to [Trelnex.Core.Data:3.0.0].

## [Trelnex.Core.Data:3.0.0] - 2025-01-02

### Added

- Added `CosmosCommandProviderFactory` to create an instance of `CosmosCommandProvider`. This now handles all CosmosClient and KeyResolver logic (with the exception of the TokenCredential).

### Changed

- Updated to dotnet 9.0.x
- Updated to [Trelnex.Core.HttpStatusCode:2.0.0].

## [Trelnex.Core.Client:2.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x
- Updated to [Trelnex.Core.HttpStatusCode:2.0.0].

## [Trelnex.Core.HttpStatusCode:2.0.0] - 2025-01-02

### Changed

- Updated to dotnet 9.0.x

## [Trelnex.Core.Api:2.0.0] - 2024-10-22

### Added

- Added `AuthenticationExtensions.NoAuthentication()` `IServiceCollection` extension method to register empty authentication and authorization policy services.
- Throw exception if authentication is not configured.

### Changed

- Updated to [Trelnex.Core.Data:2.0.0].

## [Trelnex.Core.Data.Emulator:2.0.0] - 2024-10-22

### Changed

- Updated to [Trelnex.Core.Data:2.0.0].

## [Trelnex.Core.Data:2.0.0] - 2024-10-22

### Removed

- Removed `HttpBearerToken` from `IRequestContext`.
