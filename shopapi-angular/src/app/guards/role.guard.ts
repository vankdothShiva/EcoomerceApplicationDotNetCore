// src/app/guards/role.guard.ts
// ─────────────────────────────────────────────────────
// ROLE GUARD = protects routes based on user role
//
// CONCEPT: ActivatedRouteSnapshot
//   Contains info about the current route
//   Including route.data which we use to pass roles
//
// Usage in routes:
//   {
//     path: 'admin',
//     canActivate: [authGuard, roleGuard],
//     data: { roles: ['Admin'] }  ← specify required roles
//   }
// ─────────────────────────────────────────────────────
import { inject }              from '@angular/core';
import { CanActivateFn,
         ActivatedRouteSnapshot,
         Router }              from '@angular/router';
import { TokenService }        from '../services/token.service';

export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const tokenService = inject(TokenService);
  const router       = inject(Router);

  // Read required roles from route configuration
  // Example: data: { roles: ['Admin', 'Seller'] }
  const requiredRoles: string[] = route.data['roles'] || [];

  // Get current user's roles from JWT token
  const userRoles = tokenService.getRoles();

  // Check if user has AT LEAST ONE of the required roles
  // some() = returns true if ANY element passes the test
  const hasRequiredRole = requiredRoles.some(
    role => userRoles.includes(role)
  );

  if (hasRequiredRole) {
    return true; // ✅ has required role → allow
  }

  // User doesn't have required role → go home
  router.navigate(['/']);
  return false; // ❌ block access
};