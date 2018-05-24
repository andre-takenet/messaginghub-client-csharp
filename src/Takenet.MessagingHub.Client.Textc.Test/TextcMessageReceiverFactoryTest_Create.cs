﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lime.Protocol;
using Lime.Protocol.Server;
using NUnit.Framework;
using Shouldly;
using Takenet.MessagingHub.Client.Host;
using Takenet.MessagingHub.Client.Listener;
using Takenet.MessagingHub.Client.Sender;
using Takenet.MessagingHub.Client.Test;
using Takenet.Textc;

namespace Takenet.MessagingHub.Client.Textc.Test
{
    [TestFixture]
    public class TextcMessageReceiverFactoryTest_Create
    {
        public DummyServer Server;

        [SetUp]
        public async Task SetUpAsync()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Server = new DummyServer();
            await Server.StartAsync();
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            await Server.StopAsync();
            Server.Dispose();
            TextcMessageReceiverFactory.ProcessorInstancesDictionary.Clear();
            TestCommandProcessor.Instantiated = false;
            TestCommandProcessor.InstanceCount = 0;
        }

        [Test]
        public async Task Create_With_Single_Syntax_Should_Create_Processor()
        {
            // Arrange
            var application = new Application()
            {
                MessageReceivers = new[]
                {
                    new MessageApplicationReceiver()
                    {
                        Type = typeof(TextcMessageReceiverFactory).Name,
                        Settings = new Dictionary<string, object>
                        {
                            {
                                "commands",
                                new[]
                                {
                                    new
                                    {
                                        Syntaxes = new[] { "value1:Word value2:Integer" },
                                        ProcessorType = typeof(TestCommandProcessor).Name,
                                        Method = nameof(TestCommandProcessor.ProcessAsync),
                                        ReturnText = default(string),
                                        ReturnJson  = default(Dictionary<string, object>),
                                        ReturnJsonMediaType  = default(string)
                                    },
                                    new
                                    {
                                        Syntaxes = new[] { "value1:Word value2:Integer value3:Word" },
                                        ProcessorType = typeof(TestCommandProcessor).AssemblyQualifiedName,
                                        Method = nameof(TestCommandProcessor.ProcessWithResultAsync),
                                        ReturnText = default(string),
                                        ReturnJson  = default(Dictionary<string, object>),
                                        ReturnJsonMediaType  = default(string)
                                    },
                                    new
                                    {
                                        Syntaxes = new[] { "value1:Word value2:Integer value3:Integer value4:Word(a,b,c)" },
                                        ProcessorType = default(string),
                                        Method = default(string),
                                        ReturnText = "This is an response {value1} and {value2}",
                                        ReturnJson  = default(Dictionary<string, object>),
                                        ReturnJsonMediaType  = default(string)
                                    },
                                    new
                                    {
                                        Syntaxes = new[] { "value1:Word value2:Integer value3:Integer value4:Word(a,b,c) value5:Word(x,y,z)" },
                                        ProcessorType = default(string),
                                        Method = default(string),
                                        ReturnText = default(string),
                                        ReturnJson = new Dictionary<string, object>()
                                        {
                                            { "state", "composing" }
                                        },
                                        ReturnJsonMediaType  = "application/vnd.lime.chatState+json"
                                    }
                                }
                            }
                        }
                    }
                },
                StartupType = nameof(SettingsTestStartable),
                HostName = Server.ListenerUri.Host
            };

            // Act
            var actual = await Bootstrapper.StartAsync(CancellationToken.None, application);

            // Assert
            actual.ShouldNotBeNull();
            SettingsTestStartable.Sender.ShouldNotBeNull();
            TestCommandProcessor.Instantiated.ShouldBeTrue();
            TestCommandProcessor.InstanceCount.ShouldBe(1);            
        }

        [Test]
        public async Task Create_With_Json_Single_Multiple_Syntaxes_Should_Create_Processor()
        {
            if (DateTime.Today.DayOfWeek == DayOfWeek.Saturday ||
                DateTime.Today.DayOfWeek == DayOfWeek.Sunday ||
                DateTime.Now.Hour < 6 ||
                DateTime.Now.Hour > 19)
            {
                Assert.Ignore("As this test uses hmg server, it cannot be run out of worktime!");
            }

            // Arrange
            var json =
                "{\"identifier\":\"image.search\",\"accessKey\":\"Z09SNXdt\",\"messageReceivers\":[{\"type\":\"TextcMessageReceiverFactory\",\"mediaType\":\"text/plain\",\"settings\":{\"commands\":[{\"syntaxes\":[\"[:Word(mais,more,top) top:Integer? query+:Text]\"],\"processorType\":\"TestCommandProcessor\",\"method\":\"GetImageDocumentAsync\"},{\"syntaxes\":[\"[query+:Text]\"],\"processorType\":\"TestCommandProcessor\",\"method\":\"GetFirstImageDocumentAsync\"},{\"syntaxes\":[\"[query+:Text option1:Word(a,b,c,d)]\"],\"returnText\":\"This is an return value\"},{\"syntaxes\":[\"[query+:Text option1:Word(x,y,z]\"],\"returnJson\":{\"key\":\"value1\"}}],\"scorerType\":\"MatchCountExpressionScorer\"}}],\"startupType\":\"SettingsTestStartable\",\"settings\":{\"bingApiKey\":\"z1f6I3djqJy0sWG/0HxxwjbrVrQZMF1JbTK+a5U9oNU=\"}}";

            var application = Application.ParseFromJson(json);

            // Act
            var actual = await Bootstrapper.StartAsync(CancellationToken.None, application);

            // Assert
            actual.ShouldNotBeNull();
            TestCommandProcessor.Instantiated.ShouldBeTrue();
            TestCommandProcessor.InstanceCount.ShouldBe(1);
        }
    }

    public class TestCommandProcessor
    {
        public static IMessagingHubSender Sender;
        public static IDictionary<string, object> Settings;
        public static bool Instantiated;
        public static int InstanceCount;

        public TestCommandProcessor(IMessagingHubSender sender, IDictionary<string, object> settings)
        {
            Sender = sender;
            Settings = settings;
            Instantiated = true;
            InstanceCount++;
        }

        public Task ProcessAsync(string value1, int value2, IRequestContext context)
        {
            return Task.CompletedTask;
        }

        public Task<string> ProcessWithResultAsync(string value1, int value2, string value3, IRequestContext context)
        {
            return Task.FromResult("result");
        }

        public async Task<JsonDocument> GetFirstImageDocumentAsync(string query, IRequestContext context)
        {
            return new JsonDocument();
        }

        public async Task<JsonDocument> GetImageDocumentAsync(int? top, string query, IRequestContext context)
        {
            return new JsonDocument();
        }
    }

    public class SettingsTestStartable : IStartable
    {
        public SettingsTestStartable(IMessagingHubSender sender, IDictionary<string, object> settings)
        {
            Sender = sender;
            Settings = settings;
        }

        public bool Started => _Started;

        public static bool _Started;

        public static IMessagingHubSender Sender;

        public static IDictionary<string, object> Settings;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _Started = true;
            return Task.CompletedTask;
        }
    }

}
