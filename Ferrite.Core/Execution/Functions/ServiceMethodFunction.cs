// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Linq.Expressions;
using System.Reflection;
using Ferrite.TL;

namespace Ferrite.Core.Execution.Functions;

/// <summary>
/// Builds the uniform dispatch adapter for a service method declared as
/// <c>(long authKeyId, TLBytes query) -&gt; Task&lt;TLResult&gt;</c> or
/// <c>ValueTask&lt;TLResult&gt;</c>. Reflection is used once at composition time;
/// request dispatch calls compiled delegates.
/// </summary>
public static class ServiceMethodFunction
{
    private static readonly MethodInfo CreateFactoryCoreMethod = typeof(ServiceMethodFunction)
        .GetMethod(nameof(CreateFactoryCore), BindingFlags.Static | BindingFlags.NonPublic)!;

    public static ITLFunction Create(object target, MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(target);
        return CreateFactory(method)(target);
    }

    public static Func<object, ITLFunction> CreateFactory(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        Type? serviceType = method.DeclaringType;
        if (serviceType == null)
        {
            throw new ArgumentException("Dispatchable service methods must have a declaring type.",
                nameof(method));
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (method.IsStatic || method.ContainsGenericParameters || parameters.Length != 2 ||
            parameters[0].ParameterType != typeof(long) ||
            parameters[1].ParameterType != typeof(TLBytes))
        {
            throw new ArgumentException(
                "Dispatchable service methods must have the signature (long, TLBytes).",
                nameof(method));
        }

        Type returnType = method.ReturnType;
        if (!returnType.IsGenericType)
        {
            throw UnsupportedReturnType(method);
        }

        Type returnDefinition = returnType.GetGenericTypeDefinition();
        if (returnDefinition != typeof(Task<>) && returnDefinition != typeof(ValueTask<>))
        {
            throw UnsupportedReturnType(method);
        }

        Type resultType = returnType.GetGenericArguments()[0];
        try
        {
            return (Func<object, ITLFunction>)CreateFactoryCoreMethod.MakeGenericMethod(resultType)
                .Invoke(null, [method, returnDefinition])!;
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            throw new ArgumentException(
                $"Cannot synthesize dispatch adapter for {serviceType.FullName}.{method.Name}: " +
                e.InnerException.Message, nameof(method), e.InnerException);
        }
    }

    private static Func<object, ITLFunction> CreateFactoryCore<TResult>(MethodInfo method,
        Type returnDefinition)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var authKeyId = Expression.Parameter(typeof(long), "authKeyId");
        var query = Expression.Parameter(typeof(TLBytes), "query");
        var service = Expression.Convert(target, method.DeclaringType!);
        MethodCallExpression call = Expression.Call(service, method, authKeyId, query);

        Expression invocation = call;
        if (returnDefinition == typeof(Task<>))
        {
            ConstructorInfo constructor = typeof(ValueTask<TResult>)
                .GetConstructor([typeof(Task<TResult>)])!;
            invocation = Expression.New(constructor, call);
        }

        var invoke = Expression
            .Lambda<Func<object, long, TLBytes, ValueTask<TResult>>>(
                invocation, target, authKeyId, query)
            .Compile();

        var result = Expression.Parameter(typeof(TResult), "result");
        var toBytes = Expression.Lambda<Func<TResult, TLBytes>>(
            Expression.Convert(result, typeof(TLBytes)), result).Compile();

        return instance =>
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (!method.DeclaringType!.IsInstanceOfType(instance))
            {
                throw new ArgumentException(
                    "The target does not implement the declaring service type.",
                    nameof(instance));
            }
            return new Adapter<TResult>(instance, invoke, toBytes);
        };
    }

    private static ArgumentException UnsupportedReturnType(MethodInfo method)
    {
        return new ArgumentException(
            $"Dispatchable service method {method.DeclaringType?.FullName}.{method.Name} " +
            "must return Task<T> or ValueTask<T>.", nameof(method));
    }

    private sealed class Adapter<TResult> : ITLFunction
    {
        private readonly object _target;
        private readonly Func<object, long, TLBytes, ValueTask<TResult>> _invoke;
        private readonly Func<TResult, TLBytes> _toBytes;

        public Adapter(object target, Func<object, long, TLBytes, ValueTask<TResult>> invoke,
            Func<TResult, TLBytes> toBytes)
        {
            _target = target;
            _invoke = invoke;
            _toBytes = toBytes;
        }

        public async ValueTask<TLBytes?> Process(TLBytes q, TLExecutionContext ctx)
        {
            TResult result = await _invoke(_target, ctx.CurrentAuthKeyId, q);
            try
            {
                return RpcResultGenerator.Generate(_toBytes(result), ctx.MessageId);
            }
            finally
            {
                if (result is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
