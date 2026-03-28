// src/app/interceptors/auth.interceptor.ts
// ─────────────────────────────────────────────────────
// INTERCEPTOR = middleware for HTTP requests
//
// CONCEPT: HttpInterceptor
//   Sits between your code and the server
//   Intercepts EVERY HTTP request and response
//   Like a security checkpoint
//
//   Request flow:
//   Your Code → Interceptor → Server
//                           ← Interceptor ← Server
//
// USE CASE:
//   Automatically add "Authorization: Bearer token"
//   to every API call so you don't do it manually
// ─────────────────────────────────────────────────────
import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject }                               from '@angular/core';
import { catchError, throwError }               from 'rxjs';
import { TokenService }                         from '../services/token.service';
import { Router }                               from '@angular/router';

// Angular 19 uses FUNCTIONAL interceptors (not class-based)
// This is a plain function, not a class!
export const authInterceptor: HttpInterceptorFn = (req, next) => {

  // inject() works inside interceptor functions in Angular 19
  const tokenService = inject(TokenService);
  const router       = inject(Router);

  // Get the current access token from localStorage
  const token = tokenService.getAccessToken();

  // Clone the request and add Authorization header
  // WHY CLONE? HTTP requests are immutable (can't be modified)
  // So we create a new one with the header added
  const authReq = token
    ? req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
          // Server reads this header to identify the user
        }
      })
    : req; // no token → send request as-is (for public endpoints)

  // Pass the (possibly modified) request to the next handler
  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {

      if (error.status === 401) {
        // 401 = Unauthorized
        // Token is missing, invalid, or expired
        // Clear everything and go to login
        tokenService.clearAll();
        router.navigate(['/auth/login']);
      }

      if (error.status === 403) {
        // 403 = Forbidden
        // User is logged in but doesn't have permission
        // Redirect to home
        router.navigate(['/']);
      }

      // Re-throw the error so components can handle it too
      return throwError(() => error);
    })
  );
};