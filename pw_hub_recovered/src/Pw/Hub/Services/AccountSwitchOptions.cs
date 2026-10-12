using Pw.Hub.Abstractions;

namespace Pw.Hub.Services;

public readonly record struct AccountSwitchOptions(bool CreateFreshSession, BrowserSessionIsolationMode SessionMode);
