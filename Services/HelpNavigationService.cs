namespace Mystical.WinUI.Services;

public static class HelpNavigationService
{
    public static event EventHandler<string>? TopicRequested;

    public static void RequestTopic(string sectionKey)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return;
        }

        TopicRequested?.Invoke(null, sectionKey);
    }
}
