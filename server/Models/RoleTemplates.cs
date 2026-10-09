using server.Helpers;

namespace server.Models
{
    public static class RoleTemplates
    {
        public const string OrgAdmin = "Organization Admin";
        public const string Manager = "Manager";
        public const string Employee = "Employee";
        public const string Client = "Client";

        public static readonly IReadOnlyList<string> DefaultRoleNames = new[]
        {
            OrgAdmin, Manager, Employee, Client
        };

        //permission matrix shipped with every new organization; admins can change it per role later
        public static readonly IReadOnlyDictionary<string, List<string>> Definitions =
            new Dictionary<string, List<string>>
            {
                [OrgAdmin] = PermissionKeys.All.Select(p => p.Key).ToList(),
                [Manager] = new List<string>
                {
                    PermissionKeys.ViewProducts, PermissionKeys.ManageProducts,
                    PermissionKeys.ViewCategories, PermissionKeys.ManageCategories,
                    PermissionKeys.ViewUnits, PermissionKeys.ManageUnits,
                    PermissionKeys.ViewSuppliers, PermissionKeys.ManageSuppliers,
                    PermissionKeys.ViewCustomers, PermissionKeys.ManageCustomers,
                    PermissionKeys.ViewWarehouses, PermissionKeys.ManageWarehouses,
                    PermissionKeys.ViewPO, PermissionKeys.CreatePO, PermissionKeys.EditPO, PermissionKeys.ApprovePO, PermissionKeys.CancelPO, PermissionKeys.ReceiveGRN,
                    PermissionKeys.ViewSO, PermissionKeys.CreateSO, PermissionKeys.EditSO, PermissionKeys.FulfillSO, PermissionKeys.CancelSO,
                    PermissionKeys.ViewStock, PermissionKeys.StockAdjust, PermissionKeys.StockTransfer,
                    PermissionKeys.ViewReports,
                },
                [Employee] = new List<string>
                {
                    PermissionKeys.ViewProducts,
                    PermissionKeys.ViewStock,
                    PermissionKeys.ViewPO, PermissionKeys.ReceiveGRN,
                    PermissionKeys.ViewSO, PermissionKeys.FulfillSO,
                    PermissionKeys.StockTransfer, PermissionKeys.StockAdjust,
                },
                [Client] = new List<string>
                {
                    PermissionKeys.ViewProducts,
                    PermissionKeys.ViewStock,
                    PermissionKeys.ViewReports,
                },
            };

        public static readonly Dictionary<string, string> Descriptions = new()
        {
            [OrgAdmin] = "Full control of the organization, members, roles and data.",
            [Manager] = "Runs projects, tasks and reports for their teams.",
            [Employee] = "Works on assigned projects and tasks.",
            [Client] = "Read-only stakeholder access to shared projects.",
        };

        //legacy single-role names from the pre-organization schema -> new role
        public static readonly Dictionary<string, string> LegacyMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = OrgAdmin,
            ["Manager"] = Manager,
            ["Developer"] = Employee,
        };
    }
}
