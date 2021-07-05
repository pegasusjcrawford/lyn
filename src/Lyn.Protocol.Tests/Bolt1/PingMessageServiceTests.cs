using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lyn.Protocol.Bolt1;
using Lyn.Protocol.Common;
using Lyn.Protocol.Connection;
using Lyn.Types.Bolt.Messages;
using Lyn.Types.Fundamental;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lyn.Protocol.Tests.Bolt1
{
    public class PingMessageServiceTests
    {
        private readonly PingMessageService _sut;

        private readonly Mock<ILogger<PingMessageService>> _logger;
        private readonly Mock<IDateTimeProvider> _dateTimeProvider;
        private readonly Mock<IRandomNumberGenerator> _randomNumberGenerator;
        private readonly Mock<IPingPongMessageRepository> _messageRepository;
        private readonly Mock<IBoltMessageSender<PongMessage>> _pongMessageSender;
        private readonly Mock<IBoltMessageSender<PingMessage>> _pingMessageSender;

        private DateTime _utcNow;
        private ushort _uint16;

        public PingMessageServiceTests()
        {
            _logger = new Mock<ILogger<PingMessageService>>();
            _dateTimeProvider = new Mock<IDateTimeProvider>();
            _randomNumberGenerator = new Mock<IRandomNumberGenerator>();
            _messageRepository = new Mock<IPingPongMessageRepository>();
            _pongMessageSender = new Mock<IBoltMessageSender<PongMessage>>();
            _pingMessageSender = new Mock<IBoltMessageSender<PingMessage>>();
            
            _sut = new PingMessageService(_logger.Object, _dateTimeProvider.Object,
                _randomNumberGenerator.Object, _messageRepository.Object, _pongMessageSender.Object,
                _pingMessageSender.Object);

            _utcNow = DateTime.UtcNow;

            _dateTimeProvider.Setup(_ => _.GetUtcNow())
                .Returns(() => _utcNow);

            _uint16 = RandomMessages.GetRandomNumberUInt16();

            _randomNumberGenerator.Setup(_ => _.GetUint16())
                .Returns(() => _uint16);
        }

        private static PeerMessage<PingMessage> WithPingBoltMessage(ushort numPongBytes)
        {
            return new 
            (
                RandomMessages.NewRandomPublicKey(),
                new PingMessage
                {
                    BytesLen = PingMessage.MAX_BYTES_LEN,
                    Ignored = RandomMessages.GetRandomByteArray(PingMessage.MAX_BYTES_LEN),
                    NumPongBytes = numPongBytes
                }
            );
        }

        private void ThanTheMessageWasAddedToTheRepository(PublicKey? nodeId)
        {
            _messageRepository.Verify(_ => _.AddPingMessageAsync(nodeId, _utcNow,
                It.Is<PingMessage>(_ => _.BytesLen == _uint16 % PingMessage.MAX_BYTES_LEN)));
        }

        private void ThenTheMessageWithTheWritePongLengthWasSent(PublicKey? nodeId)
        {
            _pingMessageSender.Verify(_ => _.SendMessageAsync(It.Is<PeerMessage<PingMessage>>(_
                => _.NodeId == nodeId &&
                   _.Message.NumPongBytes == PingMessage.MAX_BYTES_LEN - _uint16 % PingMessage.MAX_BYTES_LEN)));
        }
        
        [Fact]
        public async Task ProcessMessageAsyncWhenMessageIsLongerThanAllowedFailsTheMessage()
        {
            var message = WithPingBoltMessage(PingMessage.MAX_BYTES_LEN + 1);

            await _sut.ProcessMessageAsync(message);

            _pongMessageSender.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task ProcessMessageAsyncReturnsSuccessWithAPongMessage()
        {
            var message = WithPingBoltMessage(PingMessage.MAX_BYTES_LEN);

            await _sut.ProcessMessageAsync(message);

            _pongMessageSender.Verify(_ => _.SendMessageAsync(It.Is<PeerMessage<PongMessage>>(_ =>
                _.Message.BytesLen == message.Message.NumPongBytes &&
                _.Message.BytesLen == message.Message.Ignored!.Length)));
        }

        [Fact]
        public async Task ProcessMessageAsyncThrowsIfPingWasReceivedBeforeTheAllowedTime()
        {
            var message = WithPingBoltMessage(PingMessage.MAX_BYTES_LEN);

            await _sut.ProcessMessageAsync(message);

            await Assert.ThrowsAsync<ProtocolViolationException>(()
                => _sut.ProcessMessageAsync(message));
        }

        [Fact]
        public async Task CreateNewMessageAsyncReturnsPingMessageAndStoresInRepo()
        {
            var nodeId = RandomMessages.NewRandomPublicKey();

            await _sut.SendPingAsync(nodeId,CancellationToken.None);

            ThanTheMessageWasAddedToTheRepository(nodeId);

            ThenTheMessageWithTheWritePongLengthWasSent(nodeId);
        }


        [Fact]
        public async Task CreateNewMessageAsyncWhenThePingLengthExistsCreatesNewOne()
        {
            var nodeId = RandomMessages.NewRandomPublicKey();

            _messageRepository.SetupSequence(_
                    => _.PendingPingExistsForIdAsync(nodeId, (ushort) (_uint16 % PingMessage.MAX_BYTES_LEN)))
                .Returns(() => new ValueTask<bool>(true))
                .Returns(() => new ValueTask<bool>(false));

            await _sut.SendPingAsync(nodeId,CancellationToken.None);

            ThanTheMessageWasAddedToTheRepository(nodeId);

            _messageRepository.VerifyAll();

            ThenTheMessageWithTheWritePongLengthWasSent(nodeId);
        }
    }
}