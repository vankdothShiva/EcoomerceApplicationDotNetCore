// src/app/guards/auth.guard.ts
// ─────────────────────────────────────────────────────
// GUARD = protects routes from unauthorized access
//
// CONCEPT: CanActivateFn
//   Angular calls this function BEFORE loading a route
//   If returns true  → route loads normally
//   If returns false → route is blocked
//
// USE CASE:
//   Prevent unauthenticated users from accessing
//   /profile, /orders, /admin etc.
// ─────────────────────────────────────────────────────
import { inject }       from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { TokenService } from '../services/token.service';

// Functional guard (Angular 19 style)
export const authGuard: CanActivateFn = () => {
  const tokenService = inject(TokenService);
  const router       = inject(Router);

  if (tokenService.isLoggedIn()) {
    return true; // ✅ logged in → allow access
  }

  // Not logged in → redirect to login page
  router.navigate(['/auth/login']);
  return false; // ❌ block access
};