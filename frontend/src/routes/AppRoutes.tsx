import { Route, Routes } from 'react-router-dom'

import { AppLayout, NotFoundPage } from '@/components/AppLayout'
import { DashboardPage, PlaceholderPage } from '@/pages/DashboardPage'
import { LoginPage } from '@/pages/LoginPage'
import { RedirectIfAuthenticated, RequireAuth } from '@/routes/guards'

export function AppRoutes() {
  return (
    <Routes>
      <Route
        path="/login"
        element={
          <RedirectIfAuthenticated>
            <LoginPage />
          </RedirectIfAuthenticated>
        }
      />

      {/* One guarded shell; every page below inherits the session check. */}
      <Route
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route index element={<DashboardPage />} />

        <Route
          path="products"
          element={
            <PlaceholderPage
              title="Products"
              description="The product catalog: records, categories, units and pricing."
            />
          }
        />
        <Route
          path="stock"
          element={
            <PlaceholderPage
              title="Stock"
              description="Stock levels and the movement ledger that records every change."
            />
          }
        />
        <Route
          path="purchasing"
          element={
            <PlaceholderPage
              title="Purchasing"
              description="Purchase orders, receipts and supplier records."
            />
          }
        />
        <Route
          path="sales"
          element={
            <PlaceholderPage
              title="Sales"
              description="Sales orders, dispatch and invoicing."
            />
          }
        />
        <Route
          path="users"
          element={
            <PlaceholderPage
              title="Users"
              description="Accounts and role assignment. Admin only."
            />
          }
        />
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}