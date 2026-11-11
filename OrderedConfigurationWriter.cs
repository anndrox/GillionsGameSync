using System;
using System.Threading.Tasks;

namespace GillionsGameSync;

// Enqueued by the framework owner. The worker sees only detached snapshots.
// A later checkpoint can recover after a failed write, while the failed
// checkpoint still reports its own result to a caller waiting to dispatch.
internal sealed class OrderedConfigurationWriter {
    internal Task Tail { get; private set; } = Task.CompletedTask;

    internal Task Enqueue(Action write) {
        var preceding = Tail;
        return Tail = Task.Run(async () => {
            try { await preceding.ConfigureAwait(false); }
            catch (Exception) { /* This checkpoint contains the newer full state. */ }
            write();
        });
    }
}
