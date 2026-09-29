using System;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using LibraryOfRuina.infra.helpers;

namespace LibraryOfRuina.content.specialguests;

/// <summary>
/// Explicit opt-in marker for special-guest registration entry points.
/// The marked method must be static, parameterless, non-generic, and return
/// void.  The registrar never invokes methods merely named Initialize/Register.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class SpecialGuestRegistrationAttribute : Attribute
{
    public SpecialGuestRegistrationAttribute(int order = 0)
    {
        Order = order;
    }

    public int Order { get; }
}

/// <summary>
/// Discovers explicitly marked guest registration methods in this mod assembly,
/// validates all of them before invoking any, then invokes them in stable order.
/// </summary>
public static class SpecialGuestAutoRegistrar
{
    private static readonly object Sync = new();
    private static readonly HashSet<MethodInfo> CompletedMethods = [];
    private static bool _initialized;

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            MethodInfo[] methods = DiscoverRegistrationMethods();
            Validate(methods);

            foreach (MethodInfo method in methods)
            {
                if (CompletedMethods.Contains(method))
                {
                    continue;
                }

                try
                {
                    method.Invoke(null, null);
                    CompletedMethods.Add(method);
                    Log.Info($"[SpecialGuest] Registered via {Format(method)}.");
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    throw new InvalidOperationException(
                        $"Special-guest registration failed in {Format(method)}.",
                        exception.InnerException);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Special-guest registration failed in {Format(method)}.",
                        exception);
                }
            }

            _initialized = true;
        }
    }

    private static MethodInfo[] DiscoverRegistrationMethods()
    {
        return LibraryAssemblyTypes.All
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(static method => new
            {
                Method = method,
                Attribute = method.GetCustomAttribute<SpecialGuestRegistrationAttribute>(inherit: false),
            })
            .Where(static item => item.Attribute != null)
            .OrderBy(static item => item.Attribute!.Order)
            .ThenBy(static item => item.Method.DeclaringType?.FullName, StringComparer.Ordinal)
            .ThenBy(static item => item.Method.Name, StringComparer.Ordinal)
            .ThenBy(static item => item.Method.MetadataToken)
            .Select(static item => item.Method)
            .ToArray();
    }

    private static void Validate(IReadOnlyList<MethodInfo> methods)
    {
        foreach (MethodInfo method in methods)
        {
            if (!method.IsStatic
                || method.ContainsGenericParameters
                || method.ReturnType != typeof(void)
                || method.GetParameters().Length != 0)
            {
                throw new InvalidOperationException(
                    $"Invalid [SpecialGuestRegistration] method {Format(method)}. "
                    + "It must be a static, parameterless, non-generic void method.");
            }
        }
    }

    private static string Format(MethodInfo method) =>
        (method.DeclaringType?.FullName ?? "<unknown-type>") + "." + method.Name;
}
