using System;
using System.Threading.Tasks;
using Amazon;
using JustSaying.IntegrationTests.TestHandlers;
using JustSaying.Messaging.MessageHandling;
using JustSaying.TestingFramework;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace JustSaying.IntegrationTests.JustSayingFluently.MultiRegion.WithSqsTopicSubscriber
{
    [Collection(GlobalSetup.CollectionName)]
    public class WhenOneBusIsSubscribedToTwoRegions
    {
        private readonly Future<GenericMessage> _handler = new Future<GenericMessage>();

        private IHaveFulfilledPublishRequirements _primaryPublisher;
        private IHaveFulfilledPublishRequirements _secondaryPublisher;
        private IHaveFulfilledSubscriptionRequirements _subscriber;

        private GenericMessage _message1;
        private GenericMessage _message2;

        [Fact]
        public async Task MessagesPublishedToBothRegionsWillBeReceived()
        {
            var region1 = RegionEndpoint.EUWest1.SystemName;
            var region2 = RegionEndpoint.USEast1.SystemName;

            GivenASubscriptionToAQueueInTwoRegions(region1, region2);

            AndAPublisherToThePrimaryRegion(region1);
            AndAPublisherToTheSecondaryRegion(region2);

            await WhenMessagesArePublishedToBothRegions();

            await ThenTheSubscriberReceivesBothMessages();

            _subscriber.StopListening();
        }

        private void GivenASubscriptionToAQueueInTwoRegions(string primaryRegion, string secondaryRegion)
        {
            _handler.ExpectedMessageCount = 2;

            var handler = Substitute.For<IHandlerAsync<GenericMessage>>();
            handler.Handle(Arg.Any<GenericMessage>()).Returns(true);
            handler
                .When(x => x.Handle(Arg.Any<GenericMessage>()))
                .Do(async x => await _handler.Complete((GenericMessage) x.Args()[0]));

            _subscriber = CreateMeABus
                .WithLogging(new LoggerFactory())
                .InRegion(primaryRegion)
                .WithFailoverRegion(secondaryRegion)
                .WithActiveRegion(() => primaryRegion)
                .WithSqsTopicSubscriber()
                .IntoQueue("queuename")
                .WithMessageHandler(handler);
            _subscriber.StartListening();
        }

        private void AndAPublisherToThePrimaryRegion(string primaryRegion)
        {
            _primaryPublisher = CreateMeABus
                .WithLogging(new LoggerFactory())
                .InRegion(primaryRegion)
                .WithSnsMessagePublisher<GenericMessage>();
        }

        private void AndAPublisherToTheSecondaryRegion(string secondaryRegion)
        {
            _secondaryPublisher = CreateMeABus
                .WithLogging(new LoggerFactory())
                .InRegion(secondaryRegion)
                .WithSnsMessagePublisher<GenericMessage>();
        }

        private async Task WhenMessagesArePublishedToBothRegions()
        {
            _message1 = new GenericMessage { Id = Guid.NewGuid() };
            _message2 = new GenericMessage { Id = Guid.NewGuid() };

            await _primaryPublisher.PublishAsync(_message1);

            await _secondaryPublisher.PublishAsync(_message2);
        }

        private async Task ThenTheSubscriberReceivesBothMessages()
        {
            var done = await Tasks.WaitWithTimeoutAsync(_handler.DoneSignal);
            done.ShouldBeTrue();

            _handler.ReceivedMessageCount.ShouldBeGreaterThanOrEqualTo(2);
            _handler.HasReceived(_message1).ShouldBeTrue();
            _handler.HasReceived(_message2).ShouldBeTrue();
        }
    }
}
