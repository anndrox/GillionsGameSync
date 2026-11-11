# Appearance native qualification (M1a candidate)

Testing collector only. Stable has no collector or setting. Source qualification is distinct from physical acceptance: **physical check pending** on every listed tuple. No game was launched for this work.

November evidence is in [the patch qualification](native-compatibility.md#november-2026-patch-qualification).

## Slot basis

The inventory indices follow the pinned `RaptureGearsetModule.GearsetItemIndex`.
Dye offsets are relative to the slot's rendered `Human` or `Weapon` object.
Wire keys retain their existing order and names.

| Wire slot | EquippedItems index | Rendered slot | Stain0 | Stain1 |
| --- | ---: | --- | --- | --- |
| mainHand | 0 | Main-hand weapon | `0xA26` | `0xA27` |
| offHand | 1 | Off-hand weapon | `0xA26` | `0xA27` |
| head | 2 | Head | `0xA43` | `0xA44` |
| body | 3 | Top | `0xA4B` | `0xA4C` |
| hands | 4 | Arms | `0xA53` | `0xA54` |
| legs | 6 | Legs | `0xA5B` | `0xA5C` |
| feet | 7 | Feet | `0xA63` | `0xA64` |
| ears | 8 | Ear | `0xA6B` | `0xA6C` |
| neck | 9 | Neck | `0xA73` | `0xA74` |
| wrists | 10 | Wrist | `0xA7B` | `0xA7C` |
| ring1 | 11 | RFinger | `0xA83` | `0xA84` |
| ring2 | 12 | LFinger | `0xA8B` | `0xA8C` |

## Exact collector reads

Testing 0.0.100 reads the local player's rendered gear on the framework
thread while the current character has a fresh Gillions grant and the runtime
passes the exact Appearance qualification. EquippedItems provides the
underlying item and candidate glamour IDs. The Item sheet matches the rendered
model to one of those IDs.

Testing 0.0.99 used `Character.DrawData.EquipmentModelIds` and
`WeaponData[].ModelId`. These cached model IDs can match the rendered model
while their stain bytes are zero. On ClientStructs `7.57.0.9480`, the rendered
source is:

- **Armour and accessories.** `GameObject.DrawObject` at `+0x100` points to
  the human model. `Human.EquipmentModels` starts at `+0xA40`. Each entry is
  eight bytes, with model ID and variant in the first three bytes and
  `EquipmentModelId.Stain0` and `Stain1` at entry offsets `+0x03` and
  `+0x04`. The named order is head, body, hands, legs, feet, ears, neck,
  wrists, right ring and left ring. The equipped inventory map skips the belt
  and soul stone. [Human](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Scene/Human.cs),
  [DrawData and equipment slot definitions](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs).
- **Weapons.** `Character.DrawData` is at `+0x6F8`. Its weapon entries
  start at `+0x10`, with a stride of `0x70`. Use each named hand's
  `DrawData.DrawObject` pointer at entry offset `+0x18`. The rendered
  `Weapon` holds the model set, secondary ID and variant at `+0xA20`,
  `+0xA22` and `+0xA24`, and the two stains at `+0xA26` and
  `+0xA27`. [Weapon](https://github.com/aers/FFXIVClientStructs/blob/80cb4a7b7e11a6702e2eb8839afd301a918c638e/FFXIVClientStructs/FFXIV/Client/Graphics/Scene/Weapon.cs).

Glamourer uses `model.GetArmor` and `model.GetWeapons(actor)` on the
rendered human. Both projects pin Penumbra.GameData, whose `Model` helpers
read `Human.Head` as the start of the armour array and read stains directly
from the rendered weapons. The plugin uses the SDK's typed members and checks
both the draw object and model type before reading. If an equipped slot has no
matching rendered model, including a hidden model that has been removed from the
draw object, the plugin withholds the complete look. Empty inventory slots
require no rendered object. [Glamourer state reader](https://github.com/Ottermandias/Glamourer/blob/e165910345b17b99d9aa35990005b5e0f73ae65b/Glamourer/State/StateManager.cs),
[Glamourer's GameData pin](https://github.com/Ottermandias/Penumbra.GameData/blob/f463b5e670b14a66e5c9b7a74a31079ab0bba11b/Interop/Model.cs),
[Penumbra's GameData pin](https://github.com/Ottermandias/Penumbra.GameData/blob/78aa11737434e8531f8b4b8c0248b7981afdcc0f/Interop/Model.cs).

The four admitted ClientStructs pins share the same Character, GameObject,
DrawObject, DrawDataContainer and gearset slot definitions. Source comparisons
for Human, Weapon, Object and CharacterBase found constructor and animation
additions and an unrelated object type rename. The members read here and the
type query slots match. The official Dalamud 15.0.3.6, 15.0.3.7, 15.0.3.9
and 15.0.4.0 archives all report Appearance fingerprint `7d312d4f31472b10`.
The runtime list is unchanged.

Lifecycle reads: IClientState.IsLoggedIn, IsGPosing; IPlayerState.IsLoaded, ContentId (existing session identity, never added to appearance payload); ICondition indexer for BetweenAreas, BetweenAreas51, LoggingOut, Transformed, RolePlaying, Disguised. Identity/availability are rechecked after copying and before dispatch. IDataManager.GameData.Repositories["ffxiv"].Version and managed assembly versions/revisions establish the runtime. All unknown tuples stop before appearance reads.

## Pinned source comparison

The files below were read from public developer sources at the exact revisions in NativeCompatibility.cs. Each corresponding file has the same Git blob identity across the three runtime pins; generated fixed-array behavior is also covered by the pinned generator fixture. The DrawData and Character definitions also match the November pin.

| Revision | Source | Git blob |
| --- | --- | --- |
| `b666d821a47306fb447c60155b5d99377f91a5ee` | [Dalamud/Game/ClientState/Objects/Types/Character.cs](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/ClientState/Objects/Types/Character.cs) | `b2128515fe12846aa54a35d7905328e10a5251d7` |
| `b666d821a47306fb447c60155b5d99377f91a5ee` | [Dalamud/Game/ClientState/Objects/ObjectTable.cs](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/ClientState/Objects/ObjectTable.cs) | `a4a32a41df86d928d5547cf708fc213bad0a133a` |
| `b666d821a47306fb447c60155b5d99377f91a5ee` | [Dalamud/Game/ClientState/ClientState.cs](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/ClientState/ClientState.cs) | `527d0152610f20b1257cae72011bef4313ef1612` |
| `b666d821a47306fb447c60155b5d99377f91a5ee` | [Dalamud/Game/ClientState/Conditions/Condition.cs](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/ClientState/Conditions/Condition.cs) | `acad49697aa48a363e70e171425b530ef5495ab5` |
| `b666d821a47306fb447c60155b5d99377f91a5ee` | [Dalamud/Game/ClientState/Conditions/ConditionFlag.cs](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/ClientState/Conditions/ConditionFlag.cs) | `b5894d8913cc1b0fcf177cd0d99b563e37226bcc` |
| `b666d821a47306fb447c60155b5d99377f91a5ee` | [Dalamud/Game/Player/PlayerState.cs](https://github.com/goatcorp/Dalamud/blob/b666d821a47306fb447c60155b5d99377f91a5ee/Dalamud/Game/Player/PlayerState.cs) | `294a761d43e813694bbe2d9568e903c84ad379f8` |
| `313161e448e335adddd928f8a0212b2c328b5659` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs](https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs) | `d7e6861f6d879769ec3f41b4d6424a9512418ae4` |
| `313161e448e335adddd928f8a0212b2c328b5659` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryContainer.cs](https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/FFXIVClientStructs/FFXIV/Client/Game/InventoryContainer.cs) | `19ca9ae3ed407f2aa0e624304eb38e4443f8ebc4` |
| `313161e448e335adddd928f8a0212b2c328b5659` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs](https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs) | `48a45d67c145ec822eb60a17ac0bb5912f758d3c` |
| `313161e448e335adddd928f8a0212b2c328b5659` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryType.cs](https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/FFXIVClientStructs/FFXIV/Client/Game/InventoryType.cs) | `78ece1019dfa633b55067228f56950b424870ffa` |
| `313161e448e335adddd928f8a0212b2c328b5659` | [FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureGearsetModule.cs](https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureGearsetModule.cs) | `ee778a358a25818d71b1b48c7c9d97201026e9ff` |
| `313161e448e335adddd928f8a0212b2c328b5659` | [InteropGenerator.Tests/Generator/FixedSizeArrayAttributeTests.cs](https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/InteropGenerator.Tests/Generator/FixedSizeArrayAttributeTests.cs) | `b5cbeb46a5b4809edcf5f395f1f47f45414414d1` |
| `c9dacb095340ebe6513f13af6c38b13517ae70e4` | [Dalamud/Game/ClientState/Objects/Types/Character.cs](https://github.com/goatcorp/Dalamud/blob/c9dacb095340ebe6513f13af6c38b13517ae70e4/Dalamud/Game/ClientState/Objects/Types/Character.cs) | `b2128515fe12846aa54a35d7905328e10a5251d7` |
| `c9dacb095340ebe6513f13af6c38b13517ae70e4` | [Dalamud/Game/ClientState/Objects/ObjectTable.cs](https://github.com/goatcorp/Dalamud/blob/c9dacb095340ebe6513f13af6c38b13517ae70e4/Dalamud/Game/ClientState/Objects/ObjectTable.cs) | `a4a32a41df86d928d5547cf708fc213bad0a133a` |
| `c9dacb095340ebe6513f13af6c38b13517ae70e4` | [Dalamud/Game/ClientState/ClientState.cs](https://github.com/goatcorp/Dalamud/blob/c9dacb095340ebe6513f13af6c38b13517ae70e4/Dalamud/Game/ClientState/ClientState.cs) | `527d0152610f20b1257cae72011bef4313ef1612` |
| `c9dacb095340ebe6513f13af6c38b13517ae70e4` | [Dalamud/Game/ClientState/Conditions/Condition.cs](https://github.com/goatcorp/Dalamud/blob/c9dacb095340ebe6513f13af6c38b13517ae70e4/Dalamud/Game/ClientState/Conditions/Condition.cs) | `acad49697aa48a363e70e171425b530ef5495ab5` |
| `c9dacb095340ebe6513f13af6c38b13517ae70e4` | [Dalamud/Game/ClientState/Conditions/ConditionFlag.cs](https://github.com/goatcorp/Dalamud/blob/c9dacb095340ebe6513f13af6c38b13517ae70e4/Dalamud/Game/ClientState/Conditions/ConditionFlag.cs) | `b5894d8913cc1b0fcf177cd0d99b563e37226bcc` |
| `c9dacb095340ebe6513f13af6c38b13517ae70e4` | [Dalamud/Game/Player/PlayerState.cs](https://github.com/goatcorp/Dalamud/blob/c9dacb095340ebe6513f13af6c38b13517ae70e4/Dalamud/Game/Player/PlayerState.cs) | `294a761d43e813694bbe2d9568e903c84ad379f8` |
| `78975d8306c2766250ee4dca1925ef81f5dfd4f7` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs](https://github.com/aers/FFXIVClientStructs/blob/78975d8306c2766250ee4dca1925ef81f5dfd4f7/FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs) | `d7e6861f6d879769ec3f41b4d6424a9512418ae4` |
| `78975d8306c2766250ee4dca1925ef81f5dfd4f7` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryContainer.cs](https://github.com/aers/FFXIVClientStructs/blob/78975d8306c2766250ee4dca1925ef81f5dfd4f7/FFXIVClientStructs/FFXIV/Client/Game/InventoryContainer.cs) | `19ca9ae3ed407f2aa0e624304eb38e4443f8ebc4` |
| `78975d8306c2766250ee4dca1925ef81f5dfd4f7` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs](https://github.com/aers/FFXIVClientStructs/blob/78975d8306c2766250ee4dca1925ef81f5dfd4f7/FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs) | `48a45d67c145ec822eb60a17ac0bb5912f758d3c` |
| `78975d8306c2766250ee4dca1925ef81f5dfd4f7` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryType.cs](https://github.com/aers/FFXIVClientStructs/blob/78975d8306c2766250ee4dca1925ef81f5dfd4f7/FFXIVClientStructs/FFXIV/Client/Game/InventoryType.cs) | `78ece1019dfa633b55067228f56950b424870ffa` |
| `78975d8306c2766250ee4dca1925ef81f5dfd4f7` | [FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureGearsetModule.cs](https://github.com/aers/FFXIVClientStructs/blob/78975d8306c2766250ee4dca1925ef81f5dfd4f7/FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureGearsetModule.cs) | `ee778a358a25818d71b1b48c7c9d97201026e9ff` |
| `78975d8306c2766250ee4dca1925ef81f5dfd4f7` | [InteropGenerator.Tests/Generator/FixedSizeArrayAttributeTests.cs](https://github.com/aers/FFXIVClientStructs/blob/78975d8306c2766250ee4dca1925ef81f5dfd4f7/InteropGenerator.Tests/Generator/FixedSizeArrayAttributeTests.cs) | `b5cbeb46a5b4809edcf5f395f1f47f45414414d1` |
| `30509c3b4fd00edd9a3d65b7bbf16433d84be03b` | [Dalamud/Game/ClientState/Objects/Types/Character.cs](https://github.com/goatcorp/Dalamud/blob/30509c3b4fd00edd9a3d65b7bbf16433d84be03b/Dalamud/Game/ClientState/Objects/Types/Character.cs) | `b2128515fe12846aa54a35d7905328e10a5251d7` |
| `30509c3b4fd00edd9a3d65b7bbf16433d84be03b` | [Dalamud/Game/ClientState/Objects/ObjectTable.cs](https://github.com/goatcorp/Dalamud/blob/30509c3b4fd00edd9a3d65b7bbf16433d84be03b/Dalamud/Game/ClientState/Objects/ObjectTable.cs) | `a4a32a41df86d928d5547cf708fc213bad0a133a` |
| `30509c3b4fd00edd9a3d65b7bbf16433d84be03b` | [Dalamud/Game/ClientState/ClientState.cs](https://github.com/goatcorp/Dalamud/blob/30509c3b4fd00edd9a3d65b7bbf16433d84be03b/Dalamud/Game/ClientState/ClientState.cs) | `527d0152610f20b1257cae72011bef4313ef1612` |
| `30509c3b4fd00edd9a3d65b7bbf16433d84be03b` | [Dalamud/Game/ClientState/Conditions/Condition.cs](https://github.com/goatcorp/Dalamud/blob/30509c3b4fd00edd9a3d65b7bbf16433d84be03b/Dalamud/Game/ClientState/Conditions/Condition.cs) | `acad49697aa48a363e70e171425b530ef5495ab5` |
| `30509c3b4fd00edd9a3d65b7bbf16433d84be03b` | [Dalamud/Game/ClientState/Conditions/ConditionFlag.cs](https://github.com/goatcorp/Dalamud/blob/30509c3b4fd00edd9a3d65b7bbf16433d84be03b/Dalamud/Game/ClientState/Conditions/ConditionFlag.cs) | `b5894d8913cc1b0fcf177cd0d99b563e37226bcc` |
| `30509c3b4fd00edd9a3d65b7bbf16433d84be03b` | [Dalamud/Game/Player/PlayerState.cs](https://github.com/goatcorp/Dalamud/blob/30509c3b4fd00edd9a3d65b7bbf16433d84be03b/Dalamud/Game/Player/PlayerState.cs) | `294a761d43e813694bbe2d9568e903c84ad379f8` |
| `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs](https://github.com/aers/FFXIVClientStructs/blob/dd57986b8287e7c6ed9c27198f8c76cb04a81dfe/FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs) | `d7e6861f6d879769ec3f41b4d6424a9512418ae4` |
| `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryContainer.cs](https://github.com/aers/FFXIVClientStructs/blob/dd57986b8287e7c6ed9c27198f8c76cb04a81dfe/FFXIVClientStructs/FFXIV/Client/Game/InventoryContainer.cs) | `19ca9ae3ed407f2aa0e624304eb38e4443f8ebc4` |
| `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs](https://github.com/aers/FFXIVClientStructs/blob/dd57986b8287e7c6ed9c27198f8c76cb04a81dfe/FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs) | `48a45d67c145ec822eb60a17ac0bb5912f758d3c` |
| `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` | [FFXIVClientStructs/FFXIV/Client/Game/InventoryType.cs](https://github.com/aers/FFXIVClientStructs/blob/dd57986b8287e7c6ed9c27198f8c76cb04a81dfe/FFXIVClientStructs/FFXIV/Client/Game/InventoryType.cs) | `78ece1019dfa633b55067228f56950b424870ffa` |
| `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` | [FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureGearsetModule.cs](https://github.com/aers/FFXIVClientStructs/blob/dd57986b8287e7c6ed9c27198f8c76cb04a81dfe/FFXIVClientStructs/FFXIV/Client/UI/Misc/RaptureGearsetModule.cs) | `ee778a358a25818d71b1b48c7c9d97201026e9ff` |
| `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` | [InteropGenerator.Tests/Generator/FixedSizeArrayAttributeTests.cs](https://github.com/aers/FFXIVClientStructs/blob/dd57986b8287e7c6ed9c27198f8c76cb04a81dfe/InteropGenerator.Tests/Generator/FixedSizeArrayAttributeTests.cs) | `b5cbeb46a5b4809edcf5f395f1f47f45414414d1` |
| `313161e448e335adddd928f8a0212b2c328b5659` | [FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs](https://github.com/aers/FFXIVClientStructs/blob/313161e448e335adddd928f8a0212b2c328b5659/FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs) | `73b40e2651c0b8a93e709cad3532438f57b303d0` |
| `78975d8306c2766250ee4dca1925ef81f5dfd4f7` | [FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs](https://github.com/aers/FFXIVClientStructs/blob/78975d8306c2766250ee4dca1925ef81f5dfd4f7/FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs) | `73b40e2651c0b8a93e709cad3532438f57b303d0` |
| `dd57986b8287e7c6ed9c27198f8c76cb04a81dfe` | [FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs](https://github.com/aers/FFXIVClientStructs/blob/dd57986b8287e7c6ed9c27198f8c76cb04a81dfe/FFXIVClientStructs/FFXIV/Client/Game/Character/DrawDataContainer.cs) | `73b40e2651c0b8a93e709cad3532438f57b303d0` |

## Acceptance still required

Owner's later read-only physical check: loaded versus empty equipped slots, ordinary customization, base item versus applied glamour, each dye, both ring entries in recorded order, transition/logout/GPose/transformation withholding, opt-out and reconnect removal. No release is authorized here. After any unmatched game/SDK patch, appearance remains paused until requalified.
