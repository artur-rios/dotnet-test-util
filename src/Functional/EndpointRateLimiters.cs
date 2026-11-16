using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace ArturRios.Util.Test.Functional;

// ASP.NET Core's rate-limiting middleware builds a limiter for the endpoints' rate-limiting policies and never disposes
// it. That limiter runs a timer until it is disposed, and the timer holds the limiter, whose partitioner holds the
// middleware, and through the rest of the request pipeline the whole application: a host that uses UseRateLimiter()
// could never be collected, however it was disposed. Once its host is disposed, the limiter is no longer used, so
// disposing it then stops the timer and lets the host go.
//
// The middleware and its limiter are internal to Microsoft.AspNetCore.RateLimiting and found by reflection, walking the
// request pipeline from its first middleware along the delegates each middleware holds. If a version of them no
// longer matches, nothing is found and nothing is disposed: the host is then only as collectable as it was before.
// The global limiter (RateLimiterOptions.GlobalLimiter) is the API's own, possibly shared between hosts, and is left
// alone.
internal static class EndpointRateLimiters
{
    private const string MiddlewareType = "Microsoft.AspNetCore.RateLimiting.RateLimitingMiddleware";
    private const string LimiterField = "_endpointLimiter";

    public static void Release(RequestDelegate pipeline)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(pipeline);

        while (pending.TryPop(out var item))
        {
            if (!seen.Add(item))
            {
                continue;
            }

            if (item is Delegate @delegate)
            {
                foreach (var target in @delegate.GetInvocationList().Select(link => link.Target).OfType<object>())
                {
                    pending.Push(target);
                }

                continue;
            }

            foreach (var field in InstanceFields(item.GetType()))
            {
                if (item.GetType().FullName == MiddlewareType && field.Name == LimiterField)
                {
                    (field.GetValue(item) as IDisposable)?.Dispose();
                }
                else if (typeof(Delegate).IsAssignableFrom(field.FieldType) && field.GetValue(item) is { } next)
                {
                    pending.Push(next);
                }
            }
        }
    }

    private static IEnumerable<FieldInfo> InstanceFields(Type? type)
    {
        for (; type is not null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }
}
