using MediatR;

namespace Aurora.Core.Notifications;

public sealed record UpdateAvailable(string Version) : INotification;
