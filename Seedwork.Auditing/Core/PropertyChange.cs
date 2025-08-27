namespace Seedwork.Auditing.Core;

public sealed record PropertyChange(object? OldValue, object? NewValue);