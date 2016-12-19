using System;

namespace Gap
{
    public class MessageItem
    {
        public MessageItem(string from, string message)
        {
            From = from;
            Message = message;
        }
        public string From { get; set; }
        public string Message { get; set; }
    }
}
