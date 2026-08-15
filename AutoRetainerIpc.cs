using System;
using System.Linq;
using System.Reflection;
#if !GILLIONS_POLICY_TESTS
using Dalamud.Plugin;
#endif

namespace GillionsGameSync;

#if !GILLIONS_POLICY_TESTS
internal sealed class AutoRetainerIpc(IDalamudPluginInterface pluginInterface) {
    private const string AdditionalDataTypeName = "AutoRetainerAPI.Configuration.AdditionalRetainerData";

    public object? ReadAdditionalRetainerData(ulong contentId, string retainerName) {
        var dataType = FindAdditionalDataType();
        if (dataType is null) return null;
        return InvokeSubscriber(
            "AutoRetainer.GetAdditionalRetainerData",
            [typeof(ulong), typeof(string), dataType],
            "InvokeFunc",
            [contentId, retainerName]);
    }

    public void WriteAdditionalRetainerData(ulong contentId, string retainerName, object data) {
        var dataType = FindAdditionalDataType()
            ?? throw new InvalidOperationException("AutoRetainerAPI is not loaded.");
        if (!dataType.IsInstanceOfType(data))
            throw new InvalidOperationException("AutoRetainer returned an unexpected additional-data type.");
        _ = InvokeSubscriber(
            "AutoRetainer.WriteAdditionalRetainerData",
            [typeof(ulong), typeof(string), dataType, typeof(object)],
            "InvokeAction",
            [contentId, retainerName, data]);
    }

    private static Type? FindAdditionalDataType() => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(AdditionalDataTypeName, throwOnError: false, ignoreCase: false))
        .FirstOrDefault(type => type is not null);

    private object? InvokeSubscriber(string channel, Type[] genericTypes, string operation, object[] arguments) {
        var factory = typeof(IDalamudPluginInterface).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == "GetIpcSubscriber"
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length == genericTypes.Length
                && method.GetParameters().Length == 1
                && method.GetParameters()[0].ParameterType == typeof(string));
        var subscriber = factory.MakeGenericMethod(genericTypes).Invoke(pluginInterface, [channel])
            ?? throw new InvalidOperationException($"AutoRetainer IPC channel {channel} is unavailable.");
        var invocation = subscriber.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Single(method => method.Name == operation && method.GetParameters().Length == arguments.Length);
        return invocation.Invoke(subscriber, arguments);
    }
}
#endif
