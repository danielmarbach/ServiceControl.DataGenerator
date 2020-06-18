using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Faults;
using NServiceBus.Features;
using NServiceBus.Routing;
using NServiceBus.Transport;

namespace Error
{
    class ErrorGenerator : Feature
    {

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<GenerateLoad>(DependencyLifecycle.SingleInstance);
            context.RegisterStartupTask(b => b.Build<GenerateLoad>());
        }

        class GenerateLoad : FeatureStartupTask
        {
            private readonly IDispatchMessages dispatchMessages;

            public GenerateLoad(IDispatchMessages dispatchMessages)
            {
                this.dispatchMessages = dispatchMessages;
            }

            protected override async Task OnStart(IMessageSession session)
            {
                await PrepareExceptions();

                _ = Task.WhenAll(Enumerable.Range(0, NumberOfMessagesToGenerate)
                    .Select(i => Task.Run(() => Send(dispatchMessages, i))));
            }

            private static async Task PrepareExceptions()
            {
                try
                {
                    var handler = new MyHandler(ExceptionToThrow.InvalidOperationException);
                    await handler.Handle(new MyMessage(), null);
                }
                catch (Exception e)
                {
                    invalidOperationException = e;
                }

                try
                {
                    var handler = new MyHandler(ExceptionToThrow.ArgumentException);
                    await handler.Handle(new MyMessage(), null);
                }
                catch (Exception e)
                {
                    argumentException = e;
                }

                try
                {
                    var handler = new MyHandler(ExceptionToThrow.TimeoutException);
                    await handler.Handle(new MyMessage(), null);
                }
                catch (Exception e)
                {
                    timeoutException = e;
                }

                exceptions = new[] {invalidOperationException, argumentException, timeoutException};
            }

            private static byte[] content = Encoding.UTF8.GetBytes($@"<?xml version=""1.0""?>
<MyMessage xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
            xmlns=""http://tempuri.net/"">
   <MyProperty>{new string('a', 13 * 1024)}</MyProperty>
</MyMessage>");

            private static Exception invalidOperationException;
            private static Exception argumentException;
            private static Exception timeoutException;
            private static Exception[] exceptions;
            private static readonly TransportTransaction transportTransaction = new TransportTransaction();
            private static readonly ContextBag contextBag = new ContextBag();
            private static readonly SemaphoreSlim concurrencyLimiter = new SemaphoreSlim(1000);
            private static int NumberOfMessagesToGenerate = 300*1000;

            private static readonly Dictionary<string, string> reusedHeaders = new Dictionary<string, string>
            {
                { Headers.EnclosedMessageTypes, $"{typeof(MyMessage).AssemblyQualifiedName}" },
                { FaultsHeaderKeys.FailedQ, "errordatagenerator" },
                { Headers.ProcessingMachine, "ErrorDataGeneratorMachine" },
                { Headers.ProcessingEndpoint, "ErrorDataGenerator" },
                { Headers.HostId, new Guid("AC909642-C337-43A8-A593-E8DA2418D3CF").ToString("N") },
                { Headers.HostDisplayName, "ErrorDataGenerator" },
                { Headers.ContentType, "text/xml" },
                { Headers.NServiceBusVersion, "7.3.0" },
                { Headers.OriginatingEndpoint, "ErrorDataGenerator" },
                { Headers.OriginatingMachine, "ErrorDataGeneratorMachine" },
                { Headers.ReplyToAddress, "ErrorDataGenerator@ErrorDataGeneratorMachine" },
            };

            static async Task Send(IDispatchMessages dispatchMessages, int messageNumber)
            {
                try
                {
                    if (!concurrencyLimiter.Wait(0))
                    {
                        await concurrencyLimiter.WaitAsync();
                    }
                    var headers = new Dictionary<string, string>(reusedHeaders);

                    var messageId = Guid.NewGuid().ToString();
                    headers[Headers.MessageId] = messageId;
                    headers[Headers.CorrelationId] = messageId;
                    headers[Headers.ConversationId] = Guid.NewGuid().ToString();
                    headers[Headers.MessageIntent] = "Send";
                    headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);

                    ExceptionHeaderHelper.SetExceptionHeaders(headers, exceptions[messageNumber % 3]);

                    var message = new OutgoingMessage(messageId, headers, content);
                    var operation = new TransportOperation(message, new UnicastAddressTag("error"));
                    await dispatchMessages.Dispatch(new TransportOperations(operation), transportTransaction, contextBag);
                    // no overwhelm with too much output
                    if (messageNumber % 1000 == 0)
                    {
                        await Console.Error.WriteLineAsync($"|{messageNumber}|");
                    }
                }
                finally
                {
                    concurrencyLimiter.Release();
                }
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.CompletedTask;
            }
        }
    }
}