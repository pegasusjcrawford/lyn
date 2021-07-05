using Lyn.Protocol.Bolt1.Messages;
using Lyn.Types.Bolt;
using Lyn.Types.Fundamental;

namespace Lyn.Protocol.Bolt2.ChannelEstablishment.Messages
{
    public class FundingLocked : MessagePayload
    {
        public override MessageType MessageType => MessageType.FundingLocked;
        public ChannelId? ChannelId { get; set; }
        public PublicKey? NextPerCommitmentPoint { get; set; }
    }
}