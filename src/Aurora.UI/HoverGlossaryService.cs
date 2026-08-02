using Aurora.Core;

namespace Aurora.UI;

public sealed class HoverGlossaryService
{
    private HoverGlossaryWindow? _window;

    public void ShowWordDetail(string word, TranslationResult detail)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _window ??= new HoverGlossaryWindow();
            _window.ShowWordDetail(word, detail);
        });
    }
}
