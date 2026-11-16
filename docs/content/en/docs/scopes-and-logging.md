---
title: Scopes & Logging
weight: 40
description: >-
  `EnvironmentVariableScope` and `CultureScope` change process state for one test and put it back; `RecordingLogger` and `RecordingLoggerProvider` keep what the code under test logs, and `LogAssert` asserts on it.
---

This page covers the helpers that set state for one test and put it back (`EnvironmentVariableScope`,
`CultureScope`) and the recording logger (`RecordingLogger`, `RecordingLoggerProvider`, `LogAssert`). They live in
the `ArturRios.Util.Test.Scopes` and `ArturRios.Util.Test.Logging` namespaces.

## EnvironmentVariableScope

`EnvironmentVariableScope` sets and clears environment variables for as long as it lives. Disposing it puts back what
each variable held before the scope first touched it: its value, or no value at all. Use it with `using`, so the
variables are restored whether the test passes, fails or throws.

```csharp
using var environment = new EnvironmentVariableScope(
    ("MAILGUN_API_KEY", "key"),
    ("MAILGUN_DOMAIN", "mg.example.com"),
    ("MAILGUN_FROM", null));                 // null clears the variable

environment.Set("MAILGUN_API_VERSION", "v3").Unset("HTTPS_PROXY", "HTTP_PROXY");
```

| Member | Behavior |
|---|---|
| `EnvironmentVariableScope(params (string Name, string? Value)[])` | Sets each variable; a `null` value clears it. If one cannot be set, those already set are restored before the exception is thrown |
| `Set(name, value)` | Sets one variable (`null` or empty clears it); returns the scope, to chain calls |
| `Unset(params string[] names)` | Clears the variables; returns the scope |
| `Dispose()` | Restores every variable the scope changed to what it held before the scope's first change; a second call does nothing |

Environment variables belong to the process, not to the test: every test running at the same time sees the change.
A test that uses a scope must not run in parallel with a test that reads or sets the same variables, so put all such
test classes in one xUnit collection, whose tests run one at a time:

```csharp
[CollectionDefinition("Environment")]
public sealed class EnvironmentCollection;

[Collection("Environment")]
public class MailgunConfigurationTests { /* ... */ }
```

## CultureScope

`CultureScope` sets `CultureInfo.CurrentCulture` and `CultureInfo.CurrentUICulture` and puts the previous ones back
when disposed, so a culture-sensitive assertion does not depend on the machine it runs on.

```csharp
using var culture = new CultureScope("pt-BR");             // culture and UI culture
using var mixed = new CultureScope("de-DE", "en-US");      // culture, UI culture
using var invariant = new CultureScope("");                // the invariant culture

Assert.Equal("1.234,5", 1234.5.ToString("N1", CultureInfo.GetCultureInfo("pt-BR")));
```

Cultures are looked up with `CultureInfo.GetCultureInfo`, which ignores the machine user's regional overrides, so
`"pt-BR"` formats the same everywhere. The current cultures flow with the execution context, not the process: a
scope changes them for the test that created it and the work it starts (awaits, tasks, a web API host it starts),
while tests running in parallel keep theirs, so no collection is needed. Create the scope in the test itself: a
culture set inside an async helper method is undone when that method returns.

## RecordingLogger and RecordingLoggerProvider

`RecordingLogger` is an `ILogger` that keeps every entry written to it, and `RecordingLogger<T>` an `ILogger<T>`
under the category a logger factory gives `T`. Pass one where the code under test takes a logger:

```csharp
var logger = new RecordingLogger<MailgunEmailService>();
var service = new MailgunEmailService(client, logger);

await service.SendEmailAsync(message);

var entry = LogAssert.Single(logger.Entries, LogLevel.Warning);
Assert.Equal(7, entry.EventId.Id);
LogAssert.DoesNotContain(logger.Entries, "key-123");     // the API key was not logged
```

`RecordingLoggerProvider` is an `ILoggerProvider` whose loggers keep their entries in one list, for code that takes
an `ILoggerFactory` or for a whole host. Add it to a host through `WebApiHostOptions.LoggerProviders` (see
[Web API Testing](../web-api-testing/#configuring-the-host)): it then receives every entry whatever the API's
`Logging` configuration says, and keeps those at or above its own `MinimumLevel`.

```csharp
var logs = new RecordingLoggerProvider();
using var host = new WebApiHost<Program>(new WebApiHostOptions { LoggerProviders = { logs } });

await host.Gateway.PostAsync<DataOutput<SendOutput>>("/api/emails", request);

LogAssert.Single(logs.EntriesOf<EmailsController>(), LogLevel.Information, "Sent 1 email");
```

| Member | Behavior |
|---|---|
| `RecordingLogger.Entries` | The entries kept, oldest first: a snapshot that later entries do not change |
| `RecordingLogger.MinimumLevel` | The lowest level kept (default `Trace`); for a logger the provider created, the provider's |
| `RecordingLogger.Clear()` | Forgets the logger's entries |
| `RecordingLoggerProvider.Entries` | Every entry, of every category |
| `RecordingLoggerProvider.EntriesOf(category)`, `EntriesOf<T>()` | The entries of one category |
| `RecordingLoggerProvider.CreateLogger(category)`, `CreateLogger<T>()` | A logger that keeps its entries in the provider |
| `RecordingLoggerProvider.Clear()` | Forgets every entry |

The provider honours the scopes of the logger factory it is added to, and disposing it keeps its entries, so they
stay readable after the host is gone. Both are safe to log to from several threads.

Each `LogEntry` has the logger's `Category`, the `Level`, `EventId`, the rendered `Message`, the `Exception`, the
`State` the caller logged and the `Scopes` open at the time (outermost first). For the `LogInformation`-style
extensions and `[LoggerMessage]` methods, `Properties` lists the structured values, `GetProperty("Count")` returns
one, and `MessageTemplate` is the template (`"Sent {Count} emails"`).

## LogAssert

`LogAssert` asserts over any list of entries. A failure lists every entry, so the message shows what was logged
instead.

| Method | Passes when |
|---|---|
| `Single(entries, level)` | Exactly one entry has the level; returns it |
| `Single(entries, level, messageContains)` | Exactly one entry has the level and a message containing the text; returns it |
| `Contains(entries, level, messageContains)` | At least one such entry was kept; returns the first |
| `NoneAtOrAbove(entries, level)` | No entry has the level or a higher one (for example no error) |
| `DoesNotContain(entries, text)` | No message, nor the exception logged with it, contains the text (an address, a secret) |
