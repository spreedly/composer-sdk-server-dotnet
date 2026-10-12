# Spreedly Composer Server SDK for .NET

## Table of Contents

- [Purpose](#purpose)
- [Prerequisites](#prerequisites)
- [Related](#related)
- [Installation](#installation)
- [Usage](#usage)
- [API](#api)
- [Development](#development)
- [License](#license)
- [Support](#support)

## Purpose

[Spreedly Composer](https://developer.spreedly.com) enables developers to decouple security logic from their application code. This has several benefits including:

- The ability to toggle and compare security services like fraud engines, MFA, KYC, and bot prevention.
- Faster responses to new attacks. When threats evolve and new vulnerabilities are identified, your application's security logic can be updated without changing a single line of code.
- The ability to put in placeholders for future security improvements while focusing on product development.
- A way to visualize all application security logic in one place.

The Composer Server SDK for .NET makes integration with the Composer API easy and is maintained by the Spreedly team.

## Prerequisites

- .NET 8.0 or higher
- You will need to obtain an API key for your application from the Composer developer center.

## Related

Check out the [Client SDK](https://npmjs.com/package/@dodgeball/trust-sdk-client) for how to integrate Composer into your frontend applications.

[Composer examples](https://github.com/spreedly/composer-examples) has a runnable example server for this SDK in `server/dotnet`, and a web client that calls it.

## Installation

Install with the .NET CLI:

```sh
dotnet add package Spreedly.ComposerSdk.Server
```

Or with the Package Manager Console:

```powershell
Install-Package Spreedly.ComposerSdk.Server
```

## Usage

This example protects an order endpoint end to end, using an ASP.NET Core minimal API. Your API calls a checkpoint before placing the order. When the workflow needs more input from the user, such as MFA, your frontend gathers it with the Client SDK and calls your API again with the verification ID. See [Identifiers](#identifiers) for what each ID is and [useVerification](#useverification) for how the second call works.

```csharp
// In your API (Program.cs)
using System.Net;
using System.Security.Claims;
using Spreedly.ComposerSdk;

var app = WebApplication.CreateBuilder(args).Build();

// var composer = new DodgeballClient(Environment.GetEnvironmentVariable("DODGEBALL_SECRET_KEY")!); // The Dodgeball name is still supported.
var composer = new ComposerClient(Environment.GetEnvironmentVariable("SPREEDLY_SECRET_KEY")!);

// Client IP for the risk engine. Callers must not choose this value.
// Default (TRUSTED_PROXY_COUNT=0): socket address, ignore X-Forwarded-For.
// Positive count: that many hops from the right of X-Forwarded-For; the hop must be an IP.
// Set TRUSTED_PROXY_COUNT only if your app runs behind trusted reverse proxies.
static string GetIpAddress(HttpRequest request)
{
    var raw = Environment.GetEnvironmentVariable("TRUSTED_PROXY_COUNT");
    if (string.IsNullOrWhiteSpace(raw))
    {
        raw = "0";
    }
    if (!int.TryParse(raw, out var hops) || hops < 0)
    {
        throw new InvalidOperationException("Cannot derive client IP: TRUSTED_PROXY_COUNT is not a valid hop count");
    }

    var header = request.Headers["X-Forwarded-For"].FirstOrDefault() ?? "";
    var parts = header.Split(',').Select(part => part.Trim()).ToArray();

    string? candidate;
    if (hops == 0)
    {
        candidate = request.HttpContext.Connection.RemoteIpAddress?.ToString();
    }
    else if (hops <= parts.Length)
    {
        candidate = parts[parts.Length - hops];
    }
    else
    {
        candidate = null;
    }

    if (candidate is null || !IPAddress.TryParse(candidate, out _))
    {
        throw new InvalidOperationException("Cannot derive client IP: no valid IP for this TRUSTED_PROXY_COUNT");
    }
    return candidate;
}

app.MapPost("/api/orders", async (HttpRequest request, OrderRequest body) =>
{
    var checkpointResponse = await composer.CheckpointAsync(
        checkpointName: "PLACE_ORDER",
        checkpointEvent: new CheckpointEvent
        {
            Ip = GetIpAddress(request),
            Data = new Dictionary<string, object> { ["order"] = body.Order }
        },
        sessionId: request.Headers["X-Session-Id"],
        userId: request.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier),
        sourceToken: request.Headers["X-Dodgeball-Source-Token"],
        useVerificationId: request.Headers["X-Dodgeball-Verification-Id"]
    );

    if (ComposerClient.IsAllowed(checkpointResponse))
    {
        // var placedOrder = await database.CreateOrderAsync(body.Order);
        return Results.Ok(new { order = body.Order });
    }
    else if (ComposerClient.IsDenied(checkpointResponse))
    {
        // Return the verification so the frontend can show the reason
        return Results.Json(new { verification = checkpointResponse.Verification }, statusCode: 403);
    }
    else if (ComposerClient.IsRunning(checkpointResponse))
    {
        // The workflow needs more input from the user (such as MFA or KYC)
        return Results.Json(new { verification = checkpointResponse.Verification }, statusCode: 202);
    }
    else
    {
        // No verdict: decide whether to return the error, proceed, retry, or reject. See Errors.
        return Results.Json(new { message = checkpointResponse.Errors }, statusCode: 500);
    }
});

app.Run();

record Order(string ProductId, int Quantity);
record OrderRequest(Order Order);
```

```js
// In your frontend, with the Client SDK
const placeOrder = async (order, previousVerificationId = null) => {
  const sourceToken = await dodgeball.getSourceToken();

  const endpointResponse = await axios.post("/api/orders", { order }, {
    headers: {
      "x-dodgeball-source-token": sourceToken,
      "x-dodgeball-verification-id": previousVerificationId
    },
    // Let the 202 and 403 responses through instead of throwing
    validateStatus: (status) => [200, 202, 403].includes(status)
  });

  if (endpointResponse.status === 200) {
    setIsOrderPlaced(true);
    return;
  }

  dodgeball.handleVerification(endpointResponse.data.verification, {
    onVerified: async (verification) => {
      // The user completed the extra step: call your API again with the verification ID
      await placeOrder(order, verification.id);
    },
    onApproved: async () => {
      setIsOrderPlaced(true);
    },
    onDenied: async (verification) => {
      setIsOrderDenied(true);
    },
    onError: async (error) => {
      setError(error);
      setIsPlacingOrder(false);
    }
  });
}
```

## API

### Configuration

---

The SDK requires a secret API key as the first argument to the constructor.

```csharp
var composer = new ComposerClient("secret-api-key...");
```

Optionally, you can pass a `ComposerConfig` as the second argument:

```csharp
var composer = new ComposerClient("secret-api-key...", new ComposerConfig
{
    ApiUrl = "https://api.dodgeballhq.com",
    LogLevel = ComposerLogLevel.Error,
    RequestTimeoutMs = 60000
});
```

| Option | Default | Description |
| --- | --- | --- |
| `ApiVersion` | `ApiVersion.V1` | The Composer API version to use. |
| `ApiUrl` | `https://api.dodgeballhq.com` | The base URL of the Spreedly Composer API. Useful for sending requests to different environments such as `https://api.sandbox.dodgeballhq.com`. |
| `LogLevel` | `ComposerLogLevel.Info` | `Trace`, `Info`, `Error` or `None`. The SDK logs errors, plus redacted request and response payloads at `Trace`, so `Info` and `Error` currently log the same thing. The level is shared by every client in the process. See [Safe logging](#safe-logging). |
| `RequestTimeoutMs` | `30000` | How long a single HTTP request may take, in milliseconds. Applies to every call the SDK makes. A value of `0` or less is logged and replaced with the default. See [Timeouts](#timeouts). |
| `IsEnabled` | `true` | When `false`, the SDK makes no API calls: `CheckpointAsync` returns an approved response and `EventAsync` returns without sending anything. |

### Safe logging

---

The SDK writes JSON log lines to standard output. At the default `Info` level it writes no request or response payloads: a successful checkpoint logs nothing, and `Error` reports request failures. Payload logging is opt-in through `Trace`, and even there the fields that describe your end users or carry provider credentials (`ip`, `data`, `stepData` and `nextSteps`) are replaced with `[REDACTED]`, so a verbose log shows the shape of a request or response without carrying PII. Turn `Trace` on only in environments where you are comfortable with that extra detail.

### Identifiers

---

`CheckpointAsync` and `EventAsync` both take these. Provide a `sessionId`, a `sourceToken`, or both.

| Parameter | Required | Description |
| --- | --- | --- |
| `sourceToken` | Either this or `sessionId` | A token representing the device making the request. Obtained from the [Client SDK](https://npmjs.com/package/@dodgeball/trust-sdk-client). |
| `sessionId` | Either this or `sourceToken` | The current session ID of the request. |
| `userId` | No | The ID of the user in your database, once you know it (for example, after registration). |
| `useVerificationId` | No | `CheckpointAsync` only. The ID of a verification the user already completed. See [useVerification](#useverification). |

### Call a Checkpoint

---

Checkpoints represent key moments of risk in an application and are at the core of how Composer works. A checkpoint can represent any activity deemed to be a risk. Some common examples include: login, placing an order, redeeming a coupon, posting a review, changing bank account information, making a donation, transferring funds, creating a listing.

```csharp
var checkpointResponse = await composer.CheckpointAsync(
    checkpointName: "CHECKPOINT_NAME",
    checkpointEvent: new CheckpointEvent
    {
        Ip = "127.0.0.1",
        Data = new Dictionary<string, object>
        {
            ["transaction"] = new { amount = 100, currency = "USD" },
            ["paymentMethod"] = new { token = "ghi789" }
        }
    },
    sessionId: "session_def456"
);
```

Along with the [identifiers](#identifiers), `CheckpointAsync` takes:

| Parameter | Required | Description |
| --- | --- | --- |
| `checkpointName` | Yes | The name of the checkpoint to call. |
| `checkpointEvent` | Yes | A `CheckpointEvent`. |
| `checkpointEvent.Ip` | No | The IP address of the device where the request originated. Recommended whenever an end user is present; leave it empty for checkpoints with no user, such as renewals. |
| `checkpointEvent.Data` | No | Data your checkpoint's workflow uses. |
| `options` | No | A `CheckpointResponseOptions` with `Sync` (defaults to `false`) and `Timeout` (how long the SDK keeps polling an unresolved verification, in milliseconds; defaults to 10 minutes). See [Timeouts](#timeouts). |

Use named arguments: the parameter order is `(checkpointName, checkpointEvent, sessionId, userId, sourceToken, useVerificationId, options)`.

### Timeouts

---

Two settings control timing and neither one caps the other. `RequestTimeoutMs` limits how long a single HTTP request may take, and applies to every call the SDK makes. The `Timeout` in checkpoint options limits how long the SDK keeps polling for an unresolved verification, and defaults to 10 minutes. The SDK always polls at least once.

```csharp
var composer = new ComposerClient("secret-api-key...", new ComposerConfig
{
    RequestTimeoutMs = 5000 // No single HTTP call may hang longer than 5 seconds
});

var checkpointResponse = await composer.CheckpointAsync(
    checkpointName: "CHECKPOINT_NAME",
    checkpointEvent: new CheckpointEvent { Ip = "127.0.0.1" },
    sessionId: "session_def456",
    options: new CheckpointResponseOptions
    {
        Timeout = 10000 // Stop polling after 10 seconds and return whatever we have
    }
);
```

This example leaves `Sync` unset, so it defaults to `false`, which we recommend. The first request returns right away with a pending verification, and the SDK polls until it resolves, so each request is short and `RequestTimeoutMs` only guards against a hung connection. With `Sync = true`, the first request waits on the server until the workflow finishes, so `RequestTimeoutMs` must be longer than your slowest workflow. Otherwise a slow but healthy checkpoint comes back as a failure.

When the polling budget runs out, the verification is simply still pending, which is not an error: `IsRunning` returns `true`, and you can resume later by passing the verification ID to `useVerificationId`. The exception is when the last poll before the budget ran out failed: you get that failure instead, with `Success` `false`, and `HasError` returns `true`.

### Checkpoint Responses

---

Calling a checkpoint creates a verification in Composer. Its `Status` and `Outcome` determine how your application should proceed. Use the [utility methods](#utility-methods) rather than reading these fields directly.

```csharp
checkpointResponse.Success                // bool
checkpointResponse.Errors                 // List<ApiError> (Code, Message)
checkpointResponse.Version                // string
checkpointResponse.Verification           // Verification?
checkpointResponse.Verification.Id        // string
checkpointResponse.Verification.Status    // string
checkpointResponse.Verification.Outcome   // string
checkpointResponse.Verification.StepData  // Dictionary<string, object>?
checkpointResponse.Verification.NextSteps // List<Dictionary<string, object?>>?
checkpointResponse.IsTimeout              // bool
```

#### Response fields

| Property | Description |
| --- | --- |
| `Success` | `false` when no verification result could be produced. See [Errors](#errors). A verification that failed while running still has `Success` `true`. |
| `Errors` | `ApiError` objects with a `Message`, and a `Code` when the API sends one. Usually empty when `Success` is `true`. |
| `Version` | The version of the Composer API that was used to make the request. |
| `Verification` | The verification. Can be `null` when `Success` is `false`. |
| `Verification.Id` | The ID of the verification that was created. |
| `Verification.Status` | `COMPLETE`, `PENDING`, `BLOCKED` or `FAILED` (the `VerificationStatus` constants). |
| `Verification.Outcome` | `APPROVED`, `DENIED`, `PENDING`, `ERROR`, `USAGE_LIMIT` or `API_LIMIT` (the `VerificationOutcome` constants). |
| `Verification.StepData` | Data from the workflow's steps. |
| `Verification.NextSteps` | Steps the Client SDK runs next, such as MFA. Return the verification to your frontend unchanged so the Client SDK can start them right away. |
| `IsTimeout` | `true` when polling stopped after 3 failed polls in a row. See `IsTimeout` below. |

#### Statuses and outcomes

| `Status` | `Outcome` | Utility method | Meaning |
| --- | --- | --- | --- |
| `COMPLETE` | `APPROVED` | `IsAllowed` | Let the request proceed. |
| `COMPLETE` | `DENIED` | `IsDenied` | Deny the request. Return the verification to the frontend to show the reason. |
| `BLOCKED` | `PENDING` | `IsRunning` | The workflow is waiting for input from the user, such as MFA or KYC. See [useVerification](#useverification). |
| `PENDING` | `PENDING` | `IsRunning` | Still processing when the polling budget ran out. Resume later with `useVerificationId`. |
| `COMPLETE` | `PENDING` | `IsUndecided` | The workflow finished without reaching a verdict. Your application decides how to proceed. |
| `FAILED` | `ERROR` | `HasError` | The workflow failed while running, for example a third-party service was unavailable. |
| `FAILED` | `USAGE_LIMIT` | `HasError` | Your account reached its usage limit. |
| `FAILED` | `API_LIMIT` | `HasError` | Your account is sending too many requests right now, or is deactivated. |

### Utility Methods

---

Each method is static, takes a checkpoint response and returns a `bool`. The same checks are available on the response itself (`checkpointResponse.IsAllowed()` and so on), where `IsTimeout` is named `IsTimedOut()`.

| Method | Returns `true` when |
| --- | --- |
| `ComposerClient.IsAllowed(checkpointResponse)` | The request is allowed to proceed. |
| `ComposerClient.IsDenied(checkpointResponse)` | The request is denied. |
| `ComposerClient.IsRunning(checkpointResponse)` | No verdict yet: the workflow is still processing or waiting on the user. |
| `ComposerClient.IsUndecided(checkpointResponse)` | The workflow finished without a verdict. |
| `ComposerClient.HasError(checkpointResponse)` | `Success` is `false`, or the workflow failed while running. |
| `ComposerClient.IsTimeout(checkpointResponse)` | Polling stopped after 3 failed polls in a row. Running out of the polling `Timeout` is not a timeout in this sense; see [Timeouts](#timeouts). |

### Errors

---

Only invalid input throws. Everything else comes back as a response, so a checkpoint never throws because of the network or the API. The exceptions in this table are all `ComposerException`.

| What happened | Throws? | What you get | Utility method |
| --- | --- | --- | --- |
| Empty secret key, or an `ApiVersion` other than `v1` | Yes, from the constructor: `MissingConfigException` or `InvalidConfigException` | | |
| Blank `checkpointName`, `null` `checkpointEvent`, or neither `sessionId` nor `sourceToken` | Yes, from `CheckpointAsync`: `MissingParameterException` | | |
| The API rejected the request, for example there is no published workflow for the checkpoint name, or a usage or API limit was reached | No | `Success` `false`, the API's message in `Errors` | `HasError` |
| Network error, request timeout, or a response the SDK can't read | No | `Success` `false`, a `500` in `Errors` | `HasError` |
| 3 polls failed in a row | No | `Success` `false`, a `503` in `Errors` | `HasError`, `IsTimeout` |
| The workflow failed while running | No | `Success` `true`, status `FAILED`, outcome `ERROR` | `HasError` |
| Invalid input to `EventAsync` | Never | Nothing; the SDK logs it (see [Track an Event](#track-an-event)) | |

When `Success` is `false` there is no verdict at all. Don't treat it as a denial; decide whether to retry, proceed, or reject.

### useVerification

---

Sometimes the workflow needs more input from the user before it can reach a verdict. For example, if the user must complete MFA, the checkpoint response has `Status` `BLOCKED` and `Outcome` `PENDING`, and `IsRunning` returns `true`. Return the verification to your frontend and pass it to the Client SDK's `handleVerification()` method, which gathers the input from the user. When the user is done, the frontend calls your API again with the verification ID, and your API passes it to `useVerificationId`. The checkpoint then uses that verification instead of creating a new one, so the user isn't asked twice. The [Usage](#usage) example shows the full flow.

**Important Note:** To prevent replay attacks, each verification ID can only be passed to `useVerificationId` once.

### Track an Event

---

You can track additional information about a user's journey by submitting tracking events from your server. This information will be added to the user's profile and is made available to checkpoints.

```csharp
await composer.EventAsync(
    trackEvent: new TrackEvent
    {
        Type = "ADD_TO_CART",
        Data = new Dictionary<string, object>
        {
            ["product"] = new { id = "sku_123", quantity = 1 }
        }
    },
    sessionId: "session_def456"
);
```

Along with the [identifiers](#identifiers), `EventAsync` takes:

| Parameter | Required | Description |
| --- | --- | --- |
| `trackEvent` | Yes | A `TrackEvent`. |
| `trackEvent.Type` | Yes | A name for where the user is in their journey. Can be any non-blank string you choose. |
| `trackEvent.Data` | No | Data to track. |
| `trackEvent.EventTime` | No | When the event happened, in milliseconds since the epoch. Defaults to now. |

`EventAsync` doesn't throw for invalid input or failed requests and returns a `Task` with no result. The exception is `Data` that can't be serialized to JSON, which throws `JsonSerializationException`. `NotifyAsync` and `TrackAsync` are aliases. Problems are logged at `Error` instead:

- If `trackEvent` is `null`, `trackEvent.Type` is blank, or neither `sessionId` nor `sourceToken` is provided, the event is not sent and the SDK logs `Composer event was not sent: <reason>`.
- If the API rejects the event, the SDK logs `Composer event was not accepted by the API (HTTP <status>).`
- If the request fails (network error or timeout), the SDK logs `There was an error making a request.`

## Development

### Versioning

The SDK version is declared in two places that must be kept in sync when releasing:

- `src/ComposerSdk/ComposerSdk.csproj` (`Version`)
- `src/ComposerSdk/Constants.cs` (`SdkVersion`, sent in the `Composer-SDK-Version` header)

Update both when bumping the version.

### Building the Project

```sh
dotnet build
```

### Running Tests

```sh
dotnet test
```

### Running Tests with Coverage

```sh
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Packaging

```sh
dotnet pack -c Release
```

### Testing with the examples

To try local SDK changes end to end, clone [Composer examples](https://github.com/spreedly/composer-examples) next to this repository, point the `server/dotnet` example at your checkout in its `Sidecar.csproj`, and run it as the examples README describes:

```xml
<ProjectReference Include="../../../composer-sdk-server-dotnet/src/ComposerSdk/ComposerSdk.csproj" />
```

The example builds your checkout with it, so restart it after each change.

## License

This SDK is released under the Apache-2.0 License. See [LICENSE](LICENSE) for details, and [THIRD-PARTY-LICENSES](THIRD-PARTY-LICENSES) for the licenses of its dependencies.

## Support

For support, please contact:

- Email: support@spreedly.com
- Documentation: https://developer.spreedly.com
