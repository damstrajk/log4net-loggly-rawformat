using System;
using System.Text;

using log4net.Core;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace log4net.loggly
{
    internal class RawLogglyFormatter : ILogglyFormatter
    {
        private readonly Config _config;

        public RawLogglyFormatter(Config config)
        {
            _config = config;
        }

        public string ToJson(LoggingEvent loggingEvent, string renderedMessage)
        {
            var loggingInfo = JObject.Parse(renderedMessage);
            
            var resultEvent = ToJsonString(loggingInfo);
            var eventSize = Encoding.UTF8.GetByteCount(resultEvent);

            // Be optimistic regarding max event size, first serialize and then check against the limit.
            // Only if the event is bigger than allowed go back and try to trim exceeding data.
            if (eventSize <= _config.MaxEventSizeBytes) {
                return resultEvent;
            }

            var bytesOver = eventSize - _config.MaxEventSizeBytes;
            
            // ok, we are over, try to look at plain "message" and cut that down if possible
            if (loggingInfo["message"] != null)
            {
                var fullMessage = loggingInfo["message"].Value<string>();
                var originalMessageLength = fullMessage.Length;
                var newMessageLength = Math.Max(0, originalMessageLength - bytesOver);
                loggingInfo["message"] = fullMessage.Substring(0, newMessageLength);
                bytesOver -= originalMessageLength - newMessageLength;
            }

            // Message cut and still over? We can't shorten this event further, drop it,
            // otherwise it will be rejected down the line anyway and we won't be able to identify it so easily.
            if (bytesOver <= 0) {
                return ToJsonString(loggingInfo);
            }

            ErrorReporter.ReportError(
                $"LogglyFormatter: Dropping log event exceeding allowed limit of {_config.MaxEventSizeBytes} bytes. " +
                $"First 500 bytes of dropped event are: {resultEvent.Substring(0, Math.Min(500, resultEvent.Length))}");
            
            return null;
        }

        private static string ToJsonString(JObject loggingInfo)
        {
            return JsonConvert.SerializeObject(
                loggingInfo,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });
        }
    }
}
