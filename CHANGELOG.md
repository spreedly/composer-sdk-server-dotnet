# Changelog

## 1.0.0 (unreleased)

First release as `Spreedly.ComposerSdk.Server`. If you're upgrading from `Dodgeball.TrustServer` 1.0.8, read **Changed** and **Removed**.

### Added
- `ComposerConfig.RequestTimeoutMs`: a timeout for each HTTP request, default 30000 ms. A value of `0` or less is logged and replaced with the default.
- `ComposerConfig.LogLevel` (`Trace`, `Info`, `Error`, `None`; default `Info`). The SDK writes JSON log lines to stdout, and the level is shared by every client in the process. At `Trace`, `ip`, `data`, `stepData` and `nextSteps` are redacted.
- `VerificationOutcome.UsageLimit` and `VerificationOutcome.ApiLimit`. Both come back with `Success` `false`, so `HasError` is `true`.
- `EventAsync` logs an error when an event isn't sent or the API doesn't accept it.

### Changed
- The package is now `Spreedly.ComposerSdk.Server`, the namespace is `Spreedly.ComposerSdk`, and the client is `ComposerClient`: replace `new Dodgeball(...)` with `new ComposerClient(...)`. `DodgeballClient` also works.
- Requires .NET 8 or later. The license is Apache-2.0 (was MIT).
- `DodgeballConfig` is now `ComposerConfig`, with properties `ApiVersion`, `ApiUrl`, `LogLevel`, `IsEnabled` and `RequestTimeoutMs`.
- Members are PascalCase properties (`success` is `Success`, `verification.status` is `Verification.Status`), and the `VerificationStatus` and `VerificationOutcome` constants are PascalCase (`PENDING` is `Pending`).
- The constructor throws `MissingConfigException` for an empty secret key (was `ArgumentNullException`) and `InvalidConfigException` for an `ApiVersion` other than `v1`. Both are `ComposerException`.
- `PostEvent(sourceToken, sessionId, userId, event)` is now `EventAsync(trackEvent, sessionId, userId, sourceToken)`, and `Checkpoint(event, sourceToken, sessionId, userId, useVerificationId, options)` is now `CheckpointAsync(checkpointName, checkpointEvent, sessionId, userId, sourceToken, useVerificationId, options)`. Use named arguments, because the order changed. `NotifyAsync` and `TrackAsync` are aliases of `EventAsync`.
- `DodgeballEvent(type, ip, data)` is split into `CheckpointEvent { Ip, Data }`, with the checkpoint name passed separately, and `TrackEvent { Type, Data, EventTime }`. `Data` is a `Dictionary<string, object>`.
- `EventAsync` returns a `Task` with no result. It doesn't throw for invalid input or failed requests; it logs the problem and sends nothing, including when the SDK is disabled. `Data` that can't be serialized to JSON still throws.
- `CheckpointAsync` throws `MissingParameterException` for a blank `checkpointName`, a `null` `checkpointEvent`, or when both `sessionId` and `sourceToken` are blank. `Ip` is optional, and a `sourceToken` can be used instead of `sessionId`.
- `Sync` defaults to `false`.
- `Timeout` is now only how long the SDK polls, and defaults to 10 minutes. It's no longer sent to the API, and leaving it out no longer means polling with no time limit. Polling backs off from 100 ms to a 2 second ceiling.
- Polling gives up after 3 failed polls in a row and returns a `503` with `IsTimeout` `true`. `IsTimeout` is a non-nullable `bool`.
- A network failure, timeout or unreadable response returns `Success` `false` with a `500` "Internal Server Error" in `Errors`. HTTP error responses return the API's errors. `DodgeballError[]` (`category`, `message`) is now `List<ApiError>` (`Code`, `Message`).
- `Verification.StepData` is a `Dictionary<string, object>`, and `Verification.NextSteps` is a `List<Dictionary<string, object?>>`.
- The helpers are static on `ComposerClient` (`ComposerClient.IsAllowed(response)`) or instance methods on the response (`response.IsAllowed()`, `response.IsTimedOut()`). `composer.IsAllowed(response)` no longer compiles.
- `IsRunning` requires the outcome to be `PENDING` as well as the status to be `PENDING` or `BLOCKED`.
- `HasError` is `true` when `Success` is `false` or the outcome is `ERROR`.
- When disabled, the placeholder verification ID is `COMPOSER_IS_DISABLED` (was `DODGEBALL_IS_DISABLED`).

### Removed
- `CheckpointResponseOptions.Webhook`. Remove it from your checkpoint options. `CheckpointAsync` polls a pending verification as before.
- `Verification.error`.
- The `Flurl` and `Flurl.Http` dependencies. The SDK uses `HttpClient`.
