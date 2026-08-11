namespace Dalamud.Game.Text.SeStringHandling {
    public abstract class Payload;

    public sealed class SeString {
        private SeString(uint itemId, string displayName) {
            Payloads = [new Payloads.ItemPayload(itemId)];
            TextValue = displayName;
        }

        public List<Payload> Payloads { get; }
        public string TextValue { get; }

        public static SeString CreateItemLink(uint itemId, bool isHq, string displayNameOverride) =>
            new(itemId, displayNameOverride);
    }
}

namespace Dalamud.Game.Text.SeStringHandling.Payloads {
    public sealed class ItemPayload(uint itemId) : Dalamud.Game.Text.SeStringHandling.Payload {
        public uint ItemId { get; } = itemId;
    }
}
