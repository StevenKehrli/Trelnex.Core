# Trelnex.Core.HttpStatusCode

## Overview

`Trelnex.Core.HttpStatusCode` is a .NET library designed to simplify the handling of HTTP status codes.

## HttpStatusCodeException

`HttpStatusCodeException` is a custom exception to raise an error from a HTTP status code error.

```csharp
using Trelnex.Core.HttpStatusCode;

// throw an exception indicating the specified resource was not found
throw new HttpStatusCodeException(HttpStatusCode.NotFound);
```

## HttpStatusCodeExtensions

`HttpStatusCodeExtensions` creates the `ToReason` extension method for `HttpStatusCode` to convert an `HttpStatusCode` to its reason phrase.

```csharp
using Trelnex.Core.HttpStatusCode;

HttpStatusCode statusCode = HttpStatusCode.NotFound;

// reasonPhrase will be "Not Found"
string reasonPhrase = statusCode.ToReason();
```

## License

See the [LICENSE](LICENSE) file for information.
