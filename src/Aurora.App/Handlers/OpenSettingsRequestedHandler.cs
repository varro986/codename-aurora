using Aurora.Admin;
using Aurora.Core.Interfaces;
using Aurora.Core.Notifications;
using MediatR;

namespace Aurora.App.Handlers;

internal sealed class OpenSettingsRequestedHandler : INotificationHandler<OpenSettingsRequested>
{
    private readonly IAppSettings _settings;
    private readonly ISettingsWriter _writer;
    private readonly IModelManager _modelManager;

    public OpenSettingsRequestedHandler(IAppSettings settings, ISettingsWriter writer, IModelManager modelManager)
    {
        _settings = settings;
        _writer = writer;
        _modelManager = modelManager;
    }

    public Task Handle(OpenSettingsRequested notification, CancellationToken cancellationToken)
    {
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var panel = new AdminPanelWindow(_settings, _writer, _modelManager);
            panel.ShowDialog();
        });
        return Task.CompletedTask;
    }
}
