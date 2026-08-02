using MediatR;

namespace Aurora.Core.Notifications;

public sealed record TranslationReady(Aurora.Core.TranslationResult Result) : INotification;
