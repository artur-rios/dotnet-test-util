using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArturRios.Util.Test.Functional;

// WebApplicationFactory runs a minimal-hosting API's entry point on a thread of its own, and first puts in that
// thread's execution context (an AsyncLocal) a listener that holds the factory's host-building callbacks, and through
// them the factory, and the host the entry point builds. Everything the API starts on that thread captures that
// context, so a timer it never stops keeps the listener, and the factory and the host with it, alive for the rest of
// the process. The listener is only used while the host is built: releasing what it holds once the host is disposed
// leaves such a timer holding nothing but its own target.
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

    // Drops every reference the listener holds: the callbacks, the entry point's arguments and the built host.
    public static void Release(object listener)
    {
        foreach (var field in listener.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (!field.FieldType.IsValueType)
            {
                field.SetValue(listener, null);
            }
        }
    }
}
