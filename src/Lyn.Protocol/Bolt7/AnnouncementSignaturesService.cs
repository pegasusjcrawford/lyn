using System;
using System.Threading.Tasks;
using Lyn.Protocol.Connection;
using Lyn.Types.Bolt.Messages;

namespace Lyn.Protocol.Bolt7
{
   public class AnnouncementSignaturesService : IBoltMessageService<AnnouncementSignatures>
   {
      readonly IMessageValidator<AnnouncementSignatures> _messageValidator;
      private readonly IGossipRepository _repository;

      public AnnouncementSignaturesService(IMessageValidator<AnnouncementSignatures> messageValidator, 
         IGossipRepository repository)
      {
         _messageValidator = messageValidator;
         _repository = repository;
      }

      public async Task ProcessMessageAsync(PeerMessage<AnnouncementSignatures> request)
      {
         var message = request.Message;
         
         if (!_messageValidator.ValidateMessage(message))
            throw new ArgumentException(nameof(message));
         
         //TODO David - need to verify the short channel id with the funding transaction  
         
         //TODO David - add check for funding transaction announce channel bit, and received funding locked message with 6 confirmations before sending a response

         var channel = _repository.GetGossipChannel(message.ShortChannelId)
                       ?? throw new InvalidOperationException("Channel details not found in the repository"); // the channel must be added when commitment is completed

         var reply = new AnnouncementSignatures(message.ChannelId, message.ShortChannelId,null,null);// TODO get the correct signatures not the first ones 

         if (channel.ChannelAnnouncement.NodeSignature1.HasValue)
         {
            channel.ChannelAnnouncement.NodeSignature1 = message.NodeSignature;
            reply.NodeSignature = channel.ChannelAnnouncement.NodeSignature2;
         }
         else
         {
            channel.ChannelAnnouncement.NodeSignature2 = message.NodeSignature;
            reply.NodeSignature = channel.ChannelAnnouncement.NodeSignature1;
         }

         if (channel.ChannelAnnouncement.BitcoinSignature1.HasValue)
         {
            channel.ChannelAnnouncement.BitcoinSignature1 = message.BitcoinSignature;
            reply.BitcoinSignature = channel.ChannelAnnouncement.BitcoinSignature2;
         }
         else
         {
            channel.ChannelAnnouncement.BitcoinSignature2 = message.BitcoinSignature;
            reply.BitcoinSignature = channel.ChannelAnnouncement.BitcoinSignature1;
         }
         
         //TODO David - add gossip message broadcasting to all connected nodes (raise event?)
      }
   }
}