using MediatR;

namespace Aurora.Core.Notifications;

public sealed record ShutdownRequested : INotification;
