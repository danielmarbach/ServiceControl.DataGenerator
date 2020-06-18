using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace ServiceControl.DataGenerator.Audit
{
    class Program
    {
        static async Task Main()
        {
            var config = new EndpointConfiguration("servicecontrol-datagenerator-audit");
            config.SendOnly();
            config.EnableFeature<AuditGenerator>();

            var transport = config.UseTransport<MsmqTransport>();
            transport.DisablePublishing();

            var endpoint = await Endpoint.Start(config);

            await Console.Error.WriteLineAsync("Press any key to exit.");
            Console.ReadKey();

            await endpoint.Stop();
        }

        private class AuditGenerator : Feature
        {
            protected override void Setup(FeatureConfigurationContext context)
            {
                context.Container.ConfigureComponent<Foo>(DependencyLifecycle.SingleInstance);
                context.RegisterStartupTask(builder => builder.Build<Foo>());
            }

            public class Foo : FeatureStartupTask
            {
                private static readonly TransportTransaction transaction = new TransportTransaction();
                private static readonly ContextBag context = new ContextBag();
                private static readonly Dictionary<string, string> reusedHeaders = new Dictionary<string, string>
                {
                    { Headers.EnclosedMessageTypes, $"{typeof(MyMessage).AssemblyQualifiedName}" },
                    { Headers.ProcessingMachine, "AuditDataGeneratorMachine" },
                    { Headers.ProcessingEndpoint, "AuditDataGenerator" },
                    { Headers.HostId, new Guid("5C85FD81-8A4D-47CE-99BB-0800CAF85CA8").ToString("N") },
                    { Headers.HostDisplayName, "AuditDataGenerator" },
                    { Headers.ContentType, "text/xml" },
                    { Headers.NServiceBusVersion, "7.3.0" },
                    { Headers.OriginatingEndpoint, "AuditDataGenerator" },
                    { Headers.OriginatingMachine, "AuditDataGeneratorMachine" },
                    { Headers.ReplyToAddress, "AuditDataGenerator@AuditDataGeneratorMachine" },
                };

                private static readonly byte[] body = Encoding.UTF8.GetBytes($@"<?xml version=""1.0""?>
<MyMessage xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
            xmlns=""http://tempuri.net/"">
   <MyProperty>{new string('a', 13 * 1024)}</MyProperty>
</MyMessage>");

                private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();
                private readonly IDispatchMessages dispatchMessages;

                private Task sendMessages;

                public Foo(IDispatchMessages dispatchMessages) => this.dispatchMessages = dispatchMessages;

                protected override async Task OnStart(IMessageSession session)
                {
                    await Console.Error.WriteLineAsync($"Starting message sending...");
                    this.sendMessages = SendMessages(tokenSource.Token);
                }

                protected override async Task OnStop(IMessageSession session)
                {
                    await Console.Error.WriteLineAsync($"Stopping message sending...");
                    this.tokenSource.Cancel();
                    await this.sendMessages;
                    await Console.Error.WriteLineAsync($"Stopped message sending");
                }

                private async Task SendMessages(CancellationToken token)
                {
                    var batchOrdinal = 1;
                    var batchSize = 200;

                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1000);

                        await Task.WhenAll(Enumerable.Range(0, batchSize).Select(_ => Task.Run(() => SendMessage())));

                        if (batchOrdinal % 10 == 0)
                        {
                            await Console.Error.WriteLineAsync($"Sent {batchOrdinal * batchSize:N0} messages...");
                        }

                        ++batchOrdinal;
                    }
                }

                private async Task SendMessage()
                {
                    var messageId = Guid.NewGuid().ToString();

                    var headers = new Dictionary<string, string>(reusedHeaders)
                    {
                        [Headers.MessageId] = messageId,
                        [Headers.CorrelationId] = messageId,
                        [Headers.ConversationId] = Guid.NewGuid().ToString(),
                        [Headers.MessageIntent] = "Send",
                        [Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow.AddSeconds(-3)),
                        [Headers.ProcessingStarted] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow.AddSeconds(-2)),
                        [Headers.ProcessingEnded] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow.AddSeconds(-1)),
                    };

                    var message = new OutgoingMessage(messageId, headers, body);

                    var operations = new TransportOperations(new TransportOperation(message, new UnicastAddressTag("audit")));

                    await this.dispatchMessages.Dispatch(operations, transaction, context);
                }
            }

            private class MyMessage
            {
            }
        }
    }
}
