using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentry.Extensions.Logging;

namespace Infrastructure.Logging
{
    public static class SentryLoggerExtensions
    {
        public static ILoggingBuilder AddSentryLogger( this ILoggingBuilder builder, Action<SentryLoggingOptions> configure = null)
        {
            builder.AddSentry(options => 
            { 
                configure?.Invoke(options);
            });

            return builder;
        }   
    }
}
