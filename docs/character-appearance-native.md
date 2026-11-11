# Appearance native qualification (M1a candidate)

Testing collector only. Stable has no collector or setting. Source qualification is distinct from physical acceptance: **physical check pending** on every listed tuple. No game was launched for this work.

November evidence is in [the patch qualification](native-compatibility.md#november-2026-patch-qualification).

## Slot basis

Owner Amendment 1 authorizes the pinned RaptureGearsetModule.GearsetItemIndex as the index basis. Full mapping: mainHand 0, offHand 1, head 2, body 3, hands 4, legs 6, feet 7, ears 8, neck 9, wrists 10, ring1 11, ring2 12. Index 5 and index 13 are not read. Ring labels have no side meaning. Physical check pending; the owner will check the mapping read-only later.

## Exact collector reads

Testing 0.0.99 reads appearance on the framework thread while the current
character has a fresh Gillions grant and the runtime passes the exact Appearance
qualification. It copies the 26 customization bytes from the local player and
uses the 12 equipped inventory entries for the underlying item IDs. It uses the
local character's DrawData for the visible equipment and both dye IDs.

DrawData's weapon list has main hand then off hand. Its armour list has head,
body, hands, legs, feet, ears, neck, wrists, and the two rings in the same order
as the equipped inventory slots above. The read uses `Character.DrawData`,
`DrawDataContainer.WeaponData`, `EquipmentModelIds` and the models' two stain
fields. The plugin matches the drawn model to the base or glamour item in the
Item sheet. It withholds the look if the source cannot identify an equipped
slot's drawn model. The payload retains the existing base item and glamour item
fields. Glamour plates, visibility, visor and facewear stay outside this read.

The four admitted ClientStructs pins contain the same `Character.cs` blob
(`ac98b868c441befea17a85320cae4bb579e7bba3`) and the same
`DrawDataContainer.cs` blob (`73b40e2651c0b8a93e709cad3532438f57b303d0`).
The official archives for Dalamud 15.0.3.6, 15.0.3.7, 15.0.3.9 and 15.0.4.0
all report `bb864beff9836bfb` for the expanded Appearance layout.
The runtime list is the same as Testing 0.0.98.0.

Lifecycle reads: IClientState.IsLoggedIn, IsGPosing; IPlayerState.IsLoaded, ContentId (existing session identity, never added to appearance payload); ICondition indexer for BetweenAreas, BetweenAreas51, LoggingOut, Transformed, RolePlaying, Disguised. Identity/availability are rechecked after copying and before dispatch. IDataManager.GameData.Repositories["ffxiv"].Version and managed assembly versions/revisions establish the runtime. All unknown tuples stop before appearance reads.

## Pinned source comparison

The files below were read from public developer sources at the exact revisions in NativeCompatibility.cs. Each corresponding file has the same Git blob identity across the three runtime pins; generated fixed-array behavior is also covered by the pinned generator fixture. The DrawData and Character blob identities above also match the November pin.

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
