namespace DotNetCampus.ModelContextProtocol.Protocol;

/// <summary>
/// The sender or recipient of messages and data in a conversation.
/// </summary>
public enum Role
{
    /// <summary>
    /// Corresponds to a human user in the conversation.
    /// </summary>
    User,

    /// <summary>
    /// Corresponds to the AI assistant in the conversation.
    /// </summary>
    Assistant,
}
