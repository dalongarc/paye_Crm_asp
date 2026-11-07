using Paye.Core.Demo;

namespace Paye.Core.Common;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserEmail { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }

    bool IsDemoSandboxUser => DemoSandboxConstants.IsDemoUserId(UserId);

    string GetActorIdentifier() =>
        !string.IsNullOrWhiteSpace(UserId)
            ? UserId
            : !string.IsNullOrWhiteSpace(UserEmail)
                ? UserEmail
                : "system";
}
