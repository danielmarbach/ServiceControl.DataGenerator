using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NServiceBus;

namespace Error
{
    enum ExceptionToThrow
    {
        InvalidOperationException,
        TimeoutException,
        ArgumentException,
    }

    class MyHandler : IHandleMessages<MyMessage>
    {
        private ExceptionToThrow exceptionToThrow;

        public MyHandler(ExceptionToThrow exceptionToThrow)
        {
            this.exceptionToThrow = exceptionToThrow;
        }

        public async Task Handle(MyMessage message, IMessageHandlerContext context)
        {
            await Level1();
        }

        private async Task Level1()
        {
            await Level2();
        }

        private async Task Level2()
        {
            await Level3();
        }

        private async Task Level3()
        {
            await Level4();
        }


        private async Task Level4()
        {
            await Level5();
        }


        private async Task Level5()
        {
            await Level6();
        }

        private async Task Level6()
        {
            switch (exceptionToThrow)
            {
                case ExceptionToThrow.InvalidOperationException:
                    await ThrowsInvalidOperationException();
                    break;
                case ExceptionToThrow.TimeoutException:
                    await ThrowsTimeoutException();
                    break;
                case ExceptionToThrow.ArgumentException:
                    await ThrowsArgumentException();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task ThrowsInvalidOperationException()
        {
            await Task.Yield();
            throw new InvalidOperationException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task ThrowsArgumentException()
        {
            await Task.Yield();
            throw new ArgumentException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task ThrowsTimeoutException()
        {
            await Task.Yield();
            throw new TimeoutException();
        }
    }
}