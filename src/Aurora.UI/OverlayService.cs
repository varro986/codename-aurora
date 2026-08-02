using Aurora.Core;
using Aurora.Core.Interfaces;
using MediatR;

namespace Aurora.UI;

public sealed class OverlayService
{
    private readonly IPublisher _publisher;
    private readonly IAppSettings _settings;
    private OverlayWindow? _window;

    public OverlayService(IPublisher publisher, IAppSettings settings)
    {
        _publisher = publisher;
        _settings = settings;
    }

    public void ShowTranslation(TranslationResult result)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _window ??= new OverlayWindow(_publisher, _settings);
            _window.ShowTranslation(result);
        });
    }
}
