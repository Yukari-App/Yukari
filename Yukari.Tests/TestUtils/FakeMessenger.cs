using CommunityToolkit.Mvvm.Messaging;

namespace Yukari.Tests.TestUtils;

internal class FakeMessenger : IMessenger
{
    public readonly List<object> SentMessages = new List<object>();

    public void Cleanup() { }

    public T GetSingleSentMessage<T>()
        where T : class => SentMessages.OfType<T>().Single();

    public bool IsRegistered<TMessage, TToken>(object recipient, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        throw new NotImplementedException();
    }

    public void Register<TRecipient, TMessage, TToken>(
        TRecipient recipient,
        TToken token,
        MessageHandler<TRecipient, TMessage> handler
    )
        where TRecipient : class
        where TMessage : class
        where TToken : IEquatable<TToken> { }

    public void Reset() { }

    public TMessage Send<TMessage, TToken>(TMessage message, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken>
    {
        SentMessages.Add(message);
        return message;
    }

    public void Unregister<TMessage, TToken>(object recipient, TToken token)
        where TMessage : class
        where TToken : IEquatable<TToken> { }

    public void UnregisterAll(object recipient) { }

    public void UnregisterAll<TToken>(object recipient, TToken token)
        where TToken : IEquatable<TToken> { }
}
