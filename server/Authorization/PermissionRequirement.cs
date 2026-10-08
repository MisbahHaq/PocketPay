using Microsoft.AspNetCore.Authorization;

namespace server.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }
        public PermissionRequirement(string permission) => Permission = permission;
    }

    public class OrgMemberRequirement : IAuthorizationRequirement { }

    //usage: [Permission("manage_users")]
    public class PermissionAttribute : AuthorizeAttribute
    {
        public PermissionAttribute(string permission)
        {
            Policy = permission;
        }
    }

    //usage: [OrgMember] - any active user that belongs to an organization
    public class OrgMemberAttribute : AuthorizeAttribute
    {
        public OrgMemberAttribute()
        {
            Policy = PolicyNames.OrgMember;
        }
    }

    public static class PolicyNames
    {
        public const string OrgMember = "OrgMember";
    }
}
