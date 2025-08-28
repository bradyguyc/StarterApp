using CommunityToolkit.Mvvm.Messaging;

namespace MyNextBook.Models;

public class BookSelectedMessage // removed dependency on ValueChangedMessage to fix missing type
{
    public string WorkKey { get; }
    public BookSelectedMessage(string workKey) => WorkKey = workKey;
}
