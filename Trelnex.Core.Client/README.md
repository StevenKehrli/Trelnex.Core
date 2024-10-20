# Trelnex.Core.Client

## Overview

`Trelnex.Core.HttpStatusCode` is a .NET library designed to simplify HTTP client operations and their identity management.

## BaseClient

`BaseClient` is an abstract class that implements protected methods for the common HTTP request methods:

- `Task<TResponse> Delete<TResponse>()`: HTTP DELETE request
- `Task<TResponse> Get<TResponse>`: HTTP GET request
- `Task<TResponse> Patch<TRequest, TResponse>`: HTTP PATCH request
- `Task<TResponse> Post<TRequest, TResponse>`: HTTP POST request
- `Task<TResponse> Put<TRequest, TResponse>`: HTTP PUT request

A subclass can invoke these methods to handle the specifics of making the HTTP requests.

## CredentialFactory

`CredentialFactory` is responsible for creating a `TokenCredential` and an `AccessToken` for the requested scopes. Further, it will automatically refresh the `AccessToken` based on its `RefreshOn` value.

See [Azure Identity Overview](https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/?tabs=command-line) for more information.

`Trelnex.Core.Client.Identity` is integrated within `Trelnex.Core.Client` and `Trelnex.Core.Api`. The developer does not need to interact with it.

## License

See the [LICENSE](LICENSE) file for information.
