using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Mabuntle.Application.Identity;

namespace Mabuntle.Api.Notifications;

[Authorize(Policy = MabuntlePolicies.BuyerOrSeller)]
public sealed class NotificationHub : Hub
{
}
