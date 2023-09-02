using QQChatRecordArchiveConverter.Pages;
using Stylet;
using StyletIoC;

namespace QQChatRecordArchiveConverter
{
    public class Bootstrapper : Bootstrapper<MainViewModel>
    {
        protected override void ConfigureIoC(IStyletIoCBuilder builder)
        {
            // Configure the IoC container in here
        }

        protected override void Configure()
        {
            // Perform any other configuration before the application starts
        }
    }
}
