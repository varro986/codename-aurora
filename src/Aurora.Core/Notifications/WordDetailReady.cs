using MediatR;

namespace Aurora.Core.Notifications;

public sealed record WordDetailReady(string Word, Aurora.Core.TranslationResult Detail) : INotification;
