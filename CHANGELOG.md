# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- [Trelnex.Core.HttpStatusCode:2.0.0] - upgrade to net9.0.
- [Trelnex.Core.Client:2.0.0] - upgrade to net9.0.
- [Trelnex.Core.Data:3.0.0] - upgrade to net9.0.
- [Trelnex.Core.Data.Emulator:3.0.0] - upgrade to net9.0.
- [Trelnex.Core.Api:3.0.0]  - upgrade to net9.0.
- [Trelnex.Core.Data:3.x.0] - add integration tests for `CosmosCommandProvider`.
- [Trelnex.Core.Api:3.x.0] - add integration tests for `AddCosmosCommandProviders` `IServiceCollection` extension method.
- [Trelnex.Core.Data:3.x.0] - add `SqlCommandProvider` that uses an Azure SQL database as backing store.
- [Trelnex.Core.Api.Tests:3.x.0] - add integration tests for `AddSqlCommandProviders` `IServiceCollection` extension method.
- [Trelnex.Core.Data:3.x.0] - add batch transactions.

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