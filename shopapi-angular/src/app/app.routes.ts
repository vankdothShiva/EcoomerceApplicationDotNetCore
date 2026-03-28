// src/app/app.routes.ts
// ─────────────────────────────────────────────────────
// ROUTES = URL to Component mapping
//
// CONCEPT: Lazy Loading
//   loadComponent: () => import(...)
//   Component is only downloaded when user navigates to it
//   Makes initial load MUCH faster!
//
// CONCEPT: canActivate
//   Array of guards that must pass before route loads
//   All guards must return true for route to load
// ─────────────────────────────────────────────────────
import { Routes }   from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { roleGuard } from './guards/role.guard';

 export const routes: Routes = [


  // ── Default: redirect to products page ──
  {
    path: '',
    redirectTo: 'products',
    pathMatch: 'full'
  },

  // ══════════════════════════════════════
  // AUTH ROUTES (no login needed)
  // ══════════════════════════════════════
  {
    path: 'auth/login',
    // Lazy load = only download when user goes to /auth/login
    loadComponent: () =>
      import('./auth/login/login.component')
        .then(m => m.LoginComponent)
  },
  {
    path: 'auth/register',
    loadComponent: () =>
      import('./auth/register/register.component')
        .then(m => m.RegisterComponent)
  },
  {
    path: 'auth/confirm-email',
    loadComponent: () =>
      import('./auth/confirm-email/confirm-email.component')
        .then(m => m.ConfirmEmailComponent)
  },
  {
    path: 'auth/mfa',
    loadComponent: () =>
      import('./auth/mfa/mfa.component')
        .then(m => m.MfaComponent)
  },
  {
    path: 'auth/forgot-password',
    loadComponent: () =>
      import('./auth/forgot-password/forgot-password.component')
        .then(m => m.ForgotPasswordComponent)
  },

  // ══════════════════════════════════════
  // PRODUCT ROUTES (public - no login)
  // ══════════════════════════════════════
  {
    path: 'products',
    loadComponent: () =>
      import('./products/product-list/product-list.component')
        .then(m => m.ProductListComponent)
  },
  {
    path: 'products/:id',
    loadComponent: () =>
      import('./products/product-detail/product-detail.component')
        .then(m => m.ProductDetailComponent)
  },

//   // ══════════════════════════════════════
//   // PROFILE (any logged-in user)
//   // ══════════════════════════════════════
  {
    path: 'profile',
    canActivate: [authGuard], // must be logged in
    loadComponent: () =>
      import('./profile/profile.component')
        .then(m => m.ProfileComponent)
  },

//   // ══════════════════════════════════════
//   // ORDER ROUTES (Customer or Admin)
//   // ══════════════════════════════════════
  {
    path: 'orders',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Customer', 'Admin'] },
    loadComponent: () =>
      import('./orders/order-list/order-list.component')
        .then(m => m.OrderListComponent)
  },
  {
    path: 'checkout',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Customer'] },
    loadComponent: () =>
      import('./orders/checkout/checkout.component')
        .then(m => m.CheckoutComponent)
  },

//   // ══════════════════════════════════════
//   // SELLER ROUTES
//   // ══════════════════════════════════════
  {
    path: 'seller/products',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Seller', 'Admin'] },
    loadComponent: () =>
      import('./seller/seller-products/seller-products.component')
        .then(m => m.SellerProductsComponent)
  },

//   // ══════════════════════════════════════
//   // ADMIN ROUTES (Admin only)
//   // ══════════════════════════════════════
  {
    path: 'admin/dashboard', 
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./admin/dashboard/dashboard.component')
        .then(m => m.DashboardComponent)
  },
  {
    path: 'admin/users',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] },
    loadComponent: () =>
      import('./admin/users/users.component')
        .then(m => m.UsersComponent)
  },

  // ── 404: redirect to products ──
  { path: '**', redirectTo: 'products' }
 ];

