using Microsoft.AspNetCore.Authorization;
using server.Models;
using server.Services;

namespace server.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly ICurrentUser _current;

        public PermissionHandler(ICurrentUser current)
        {
            _current = current;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var user = await _current.LoadAsync();

            if (user is null || user.Status != UserStatus.Active || user.OrganizationId is null)
            {
                context.Fail();
                return;
            }

            if (_current.Can(requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
    }

    public class OrgMemberHandler : AuthorizationHandler<OrgMemberRequirement>
    {
        private readonly ICurrentUser _current;

        public OrgMemberHandler(ICurrentUser current)
        {
            _current = current;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, OrgMemberRequirement requirement)
        {
            var user = await _current.LoadAsync();

            if (user is not null && user.Status == UserStatus.Active && user.OrganizationId is not null)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
    }
}
