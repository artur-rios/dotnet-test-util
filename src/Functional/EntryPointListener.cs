using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArturRios.Util.Test.Functional;

// WebApplicationFactory runs a minimal-hosting API's entry point on a thread of its own, and first puts in that
// thread's execution context (an AsyncLocal) a listener that holds the factory's host-building callbacks, and through
// them the factory, and the host the entry point builds. Everything the API starts on that thread captures that
// context, so a timer it never stops keeps the listener, and the factory and the host with it, alive for the rest of
// the process. Releasing what the listener holds once the host is disposed leaves such a timer holding nothing but
// its own target.
//
// The listener is internal to Microsoft.AspNetCore.Mvc.Testing and found by reflection. If a version of it no longer
// matches, Current finds nothing and Release does nothing: the host is then only as collectable as it was before.
internal static class EntryPointListener
{
    private static readonly object? CurrentListener = typeof(WebApplicationFactoryClientOptions).Assembly
        .GetType("Microsoft.Extensions.Hosting.HostFactoryResolver+HostingListener")
        ?.GetField("_currentListener", BindingFlags.NonPublic | BindingFlags.Static)
        ?.GetValue(null);

    private static readonly PropertyInfo? Value = CurrentListener?.GetType().GetProperty(nameof(AsyncLocal<>.Value));

    // The listener of the entry point running on the calling thread, or null: call it from a host-building callback,
    // which runs on that thread.
    public static object? Current() => Value?.GetValue(CurrentListener);

    // Drops what the listener holds that reaches the factory and the host. The entry point's thread still runs when the
    // host is disposed, and uses the listener again once Main returns: it calls its callbacks only when they are set,
    // so they become null, but completes the completion source of the built host unconditionally, so that one is
    // replaced by one already completed.
    public static void Release(object listener)
    {
        foreach (var field in listener.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                field.SetValue(listener, null);
            }
            else if (field.FieldType == typeof(TaskCompletionSource<object>))
            {
                field.SetValue(listener, Completed());
            }
        }
    }

    private static readonly object Released = new();

    private static TaskCompletionSource<object> Completed()
    {
        var completed = new TaskCompletionSource<object>();
        completed.SetResult(Released);

        return completed;
    }
}
