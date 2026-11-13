# Desynthesis native source review

Testing 0.0.104.0 admits the desynthesis reader on one exact runtime. Game
`2026.11.03.0000.0000`, Dalamud
`15.0.4.0+12e318c1936e449e5883faa61214eb2ced478349`, ClientStructs
`7.57.0.9480+80cb4a7b7e11a6702e2eb8839afd301a918c638e`, Lumina `7.7.0` and
Lumina.Excel `7.5.1`. The read enters through
`SupportsRuntime(NativeFamily.Desynthesis, game.Version)`. Stable does not
compile the reader.

| Read | Layout at `80cb4a7b` | Pinned source |
|---|---|---|
| Salvage agent | `AgentId.Salvage = 129`. `AgentSalvage` has the corresponding agent attribute, inherits `AgentInterface` and has size `0x470`. | [AgentModule](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentModule.cs), [AgentSalvage](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentSalvage.cs) |
| Source and outputs | The source is the `uint` at `0x444`. The generated `DesynthResults` span covers three `0x08` records starting at `0x448`. Each record has a `uint ItemId` at `+0x00` and an `int Quantity` at `+0x04`. | [AgentSalvage and SalvageResult](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentSalvage.cs) |
| Single window | `SalvageResult` is an addon identity. The reader uses its lifecycle events and reads the result from the agent. | [AddonSalvageResult](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/UI/AddonSalvageResult.cs) |
| Bulk window | `SalvageAutoDialog` is size `0x248`, with `AtkUnitBase` at `0x00` and the `bool IsDesynthesizing` at `0x240`. `IsVisible` tests `Show` (value `2`) in bits 20 through 23 of the `uint Flags198` at `0x198`. | [AddonSalvageAutoDialog](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/UI/AddonSalvageAutoDialog.cs), [AtkUnitBase](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Component/GUI/AtkUnitBase.cs) |

The collector uses Dalamud's addon lifecycle notifications and game GUI
lookups on the framework thread. The ClientStructs accesses are reads of the
fields above and the generated span and visibility getters. There are no new
hooks, signatures or game writes.

## Native parity

The repository's `--native-layout-check` on the pinned 15.0.4.0 runtime
matched every recorded fingerprint before and after the runtime table change.

| Family | Fingerprint |
|---|---|
| Hunt | `2312d897d65b4177` |
| Submarine | `8c4e37e328c571a9` |
| Travel | `22c25911f7cc88bf` |
| FATE | `6923c1e09b662322` |
| Dashboard | `72e3d331203627af` |
| Beastmaster | `7984e5ebc96f48c5` |
| Appearance | `555794b7a8157af9` |
| Desynthesis | `e25c169d60f8290b` |

`NativeCompatibility.cs` appends `Desynthesis` and lists November for it.
The existing enum values, runtime tuples and qualification lists are
unchanged. `NativeCompatibility.Runtime.cs`, `NativeObservationPolicy.cs`
and `NativeProduct.cs` are byte-identical to the branch baseline.

The desynthesis fingerprint reproduces the branch's first layout baseline.
There is no earlier qualified desynthesis runtime. Synthetic fixtures check
offsets, decoding and the bulk lifecycle state transitions. Physical checks
for one single result and one event per completed item in a bulk run remain
pending, as does Site admission.
