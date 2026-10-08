namespace server.Helpers
{
    public static class PermissionKeys
    {
        public const string ManageOrganization = "manage_organization";
        public const string ManageUsers = "manage_users";
        public const string InviteUsers = "invite_users";
        public const string RemoveUsers = "remove_users";
        public const string ManageRoles = "manage_roles";
        public const string CreateProject = "create_project";
        public const string EditProject = "edit_project";
        public const string DeleteProject = "delete_project";
        public const string ViewProject = "view_project";
        public const string CreateTask = "create_task";
        public const string AssignTask = "assign_task";
        public const string EditTask = "edit_task";
        public const string DeleteTask = "delete_task";
        public const string ViewReports = "view_reports";
        public const string ManageFiles = "manage_files";
        public const string CommentOnTask = "comment_on_task";

        public static readonly IReadOnlyList<(string Key, string Description)> All = new List<(string, string)>
        {
            (ManageOrganization, "Manage organization settings"),
            (ManageUsers, "Invite, deactivate and manage users"),
            (InviteUsers, "Invite employees to the organization"),
            (RemoveUsers, "Remove users from the organization"),
            (ManageRoles, "Create roles and configure role permissions"),
            (CreateProject, "Create projects"),
            (EditProject, "Edit projects"),
            (DeleteProject, "Delete projects"),
            (ViewProject, "View projects"),
            (CreateTask, "Create tasks"),
            (AssignTask, "Assign tasks to members"),
            (EditTask, "Edit tasks"),
            (DeleteTask, "Delete tasks"),
            (ViewReports, "View project reports"),
            (ManageFiles, "Upload and manage files"),
            (CommentOnTask, "Comment on tasks"),
        };
    }
}
