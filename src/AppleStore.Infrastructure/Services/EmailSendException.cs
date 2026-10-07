namespace AppleStore.Infrastructure.Services;

// Thrown by an IEmailSender when a message could not be handed to the mail
// server. Callers turn it into a message the visitor can act on (try again).
public class EmailSendException : Exception
{
    public EmailSendException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
