"""Run actual managed Hunt presence/authority/sender paths with in-process I/O.

Extract complete methods from today's source into ignored generated files. The
fixture links the complete permissions, retention and preparation modules; it
does not maintain a second implementation of the changed production methods.
No package sources, sockets, native runtime or installed plugin are required.
"""
from pathlib import Path
import hashlib
import json
import subprocess

root = Path(__file__).resolve().parents[1]
project = root / 'tests/GillionsGameSync.HuntRecoveryTests'
generated = project / 'obj/generated'
generated.mkdir(parents=True, exist_ok=True)


def method(source, anchor):
    start = source.index(anchor)
    end = source.index('\n    }', start) + 6
    return source[start:end] + '\n'


source = (root / 'Plugin.cs').read_text(encoding='utf-8')
methods = [method(source, anchor) for anchor in [
    '    private async Task SendRetainerPresenceAsync(',
    '    private void ClearRetainerServerAcceptance()',
    '    private bool CoveragePreparedCurrent(',
    '    private async Task SendPersonalAsync(',
]]
start = source.index('    private bool PersonalEnabled(')
end = source.index('    private void SetPersonalSync(', start)
methods.append(source[start:end])
(generated / 'PluginPaths.cs').write_text('''// Generated from complete current production methods; do not edit.
using System; using System.Linq; using System.Net.Http; using System.Text; using System.Text.Json;
using System.Diagnostics; using System.Threading; using System.Threading.Tasks;
namespace GillionsGameSync;
public sealed partial class Plugin {
''' + '\n'.join(methods) + '\n}\n', encoding='utf-8')

source = (root / 'SyncOwnership.cs').read_text(encoding='utf-8')
(generated / 'Lifetime.cs').write_text(
    source[:source.index('public sealed class OwnedCharacterState')]
    + source[source.index('public enum SyncRequestMode'):], encoding='utf-8')
source = (root / 'SyncTransportPolicy.cs').read_text(encoding='utf-8')
(generated / 'ResponseReader.cs').write_text('''using System; using System.IO; using System.Net.Http; using System.Text;
using System.Text.Json; using System.Threading; using System.Threading.Tasks;
namespace GillionsGameSync;
public static class SyncResponsePolicy {
 public const int MaximumBytes = 64 * 1024;
 public const int MaximumDepth = 16;
''' + method(source, '    public static async Task<string> ReadAsync(') + '}\n', encoding='utf-8')
source = (root / 'RetainerPresence.cs').read_text(encoding='utf-8')
(generated / 'PresenceResponse.cs').write_text(
    'using System; using System.Text.Json;\nnamespace GillionsGameSync;\n'
    + source[source.index('public static class RetainerPresenceResponsePolicy'):], encoding='utf-8')
(generated / 'method-hashes.json').write_text(json.dumps({
    m.splitlines()[0].strip(): hashlib.sha256(m.encode()).hexdigest() for m in methods
}, indent=2) + '\n', encoding='utf-8')

config = generated / 'NuGet.Config'
config.write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
csproj = project / 'GillionsGameSync.HuntRecoveryTests.csproj'
subprocess.run(['dotnet', 'restore', str(csproj), '--configfile', str(config)], check=True)
subprocess.run(['dotnet', 'build', str(csproj), '-c', 'Release', '--no-restore',
                '-p:UseSharedCompilation=false', '--nologo'], check=True)
subprocess.run(['dotnet', str(project / 'bin/Release/net10.0-windows/GillionsGameSync.HuntRecoveryTests.dll'),
                str(project / 'fixtures')], check=True)
