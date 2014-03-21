﻿using System;
using System.Threading;
using Amazon;
using JustEat.Simples.NotificationStack.Messaging.MessageHandling;
using JustEat.Simples.NotificationStack.Messaging.Monitoring;
using JustEat.Testing;
using NSubstitute;
using NUnit.Framework;
using SimpleMessageMule;
using Tests.MessageStubs;

namespace NotificationStack.IntegrationTests.FluentNotificationStackTests
{
    [TestFixture]
    public class WhenAHandlerThrowsAnException
    {
        private readonly IHandler<GenericMessage> _handler = Substitute.For<IHandler<GenericMessage>>();
        private IFluentMessageMule _publisher;
        private Action<Exception> _globalErrorHandler;
        private bool _handledException;
        private IMessageMonitor _monitoring;

        [SetUp]
        public void Given()
        {
            _handler.Handle(Arg.Any<GenericMessage>()).Returns(true).AndDoes(ex => { throw new Exception("My Ex"); });
            _globalErrorHandler = ex => { _handledException = true; };
            _monitoring = Substitute.For<IMessageMonitor>();
            var publisher = FluentMessagingMule.Register(c =>
                                                                        {
                                                                            c.PublishFailureBackoffMilliseconds = 1;
                                                                            c.PublishFailureReAttempts = 3;
                                                                            c.Region = RegionEndpoint.EUWest1.SystemName;
                                                                        })
                                                                        .WithMonitoring(_monitoring)
                .WithSnsMessagePublisher<GenericMessage>("CustomerCommunication")
                .WithSqsTopicSubscriber("CustomerCommunication", 60, instancePosition: 1, onError: _globalErrorHandler)
                .WithMessageHandler(_handler);

            publisher.StartListening();
            _publisher = publisher;
        }

        [Then]
        public void CustomExceptionHandlingIsCalled()
        {
            _publisher.Publish(new GenericMessage());
            Thread.Sleep(1000);

            _handler.Received().Handle(Arg.Any<GenericMessage>());
            Assert.That(_handledException, Is.EqualTo(true));
        }

        [TearDown]
        public void ByeBye()
        {
            _publisher.StopListening();
            _publisher = null;
        }
    }
}
