using MediatR;

namespace Aurora.Core.Notifications;

public sealed record WordDetailRequested(string Word) : INotification;
