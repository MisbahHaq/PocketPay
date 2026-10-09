namespace server.Helpers
{
    public static class PermissionKeys
    {
        // Org/Users/Roles
        public const string ManageOrganization = "manage_organization";
        public const string ManageUsers = "manage_users";
        public const string InviteUsers = "invite_users";
        public const string RemoveUsers = "remove_users";
        public const string ManageRoles = "manage_roles";

        // Masters
        public const string ViewProducts = "view_products";
        public const string ManageProducts = "manage_products";
        public const string ViewCategories = "view_categories";
        public const string ManageCategories = "manage_categories";
        public const string ViewUnits = "view_units";
        public const string ManageUnits = "manage_units";
        public const string ViewSuppliers = "view_suppliers";
        public const string ManageSuppliers = "manage_suppliers";
        public const string ViewCustomers = "view_customers";
        public const string ManageCustomers = "manage_customers";
        public const string ViewWarehouses = "view_warehouses";
        public const string ManageWarehouses = "manage_warehouses";

        // Purchases
        public const string ViewPO = "view_po";
        public const string CreatePO = "create_po";
        public const string EditPO = "edit_po";
        public const string ApprovePO = "approve_po";
        public const string CancelPO = "cancel_po";
        public const string ReceiveGRN = "receive_grn";

        // Sales
        public const string ViewSO = "view_so";
        public const string CreateSO = "create_so";
        public const string EditSO = "edit_so";
        public const string FulfillSO = "fulfill_so";
        public const string CancelSO = "cancel_so";

        // Stock
        public const string ViewStock = "view_stock";
        public const string StockAdjust = "stock_adjust";
        public const string StockTransfer = "stock_transfer";

        // Reports/Audit
        public const string ViewReports = "view_reports";
        public const string ViewAudit = "view_audit";

        public static readonly IReadOnlyList<(string Key, string Description)> All = new List<(string, string)>
        {
            (ManageOrganization, "Manage organization settings"),
            (ManageUsers, "Manage users"),
            (InviteUsers, "Invite users"),
            (RemoveUsers, "Remove users"),
            (ManageRoles, "Manage roles"),
            (ViewProducts, "View products"),
            (ManageProducts, "Manage products"),
            (ViewCategories, "View categories"),
            (ManageCategories, "Manage categories"),
            (ViewUnits, "View units"),
            (ManageUnits, "Manage units"),
            (ViewSuppliers, "View suppliers"),
            (ManageSuppliers, "Manage suppliers"),
            (ViewCustomers, "View customers"),
            (ManageCustomers, "Manage customers"),
            (ViewWarehouses, "View warehouses"),
            (ManageWarehouses, "Manage warehouses"),
            (ViewPO, "View purchase orders"),
            (CreatePO, "Create purchase orders"),
            (EditPO, "Edit purchase orders"),
            (ApprovePO, "Approve purchase orders"),
            (CancelPO, "Cancel purchase orders"),
            (ReceiveGRN, "Receive goods"),
            (ViewSO, "View sales orders"),
            (CreateSO, "Create sales orders"),
            (EditSO, "Edit sales orders"),
            (FulfillSO, "Fulfill sales orders"),
            (CancelSO, "Cancel sales orders"),
            (ViewStock, "View stock"),
            (StockAdjust, "Stock adjustments"),
            (StockTransfer, "Stock transfers"),
            (ViewReports, "View reports"),
            (ViewAudit, "View audit log"),
        };
    }
}
