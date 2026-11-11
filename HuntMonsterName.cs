using System;

namespace GillionsGameSync;

internal static class HuntMonsterName {
    internal static string Display(string name) => string.Create(name.Length, name, static (result, source) => {
        for (var i = 0; i < source.Length; i++)
            result[i] = i == 0 || source[i - 1] == ' ' ? char.ToUpperInvariant(source[i]) : source[i];
    });
}
