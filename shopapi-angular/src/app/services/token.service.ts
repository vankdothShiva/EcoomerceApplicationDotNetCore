// src/app/services/token.service.ts
// ─────────────────────────────────────────────────────
// TOKEN SERVICE = manages JWT token storage
//
// CONCEPT: localStorage
//   Browser storage that persists even after page refresh
//   We store JWT here so user stays logged in
//
// CONCEPT: Injectable({ providedIn: 'root' })
//   Makes this service a singleton
//   Only ONE instance exists for entire app
//   Any component can inject and use it
// ─────────────────────────────────────────────────────
import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root' // available everywhere in the app
})
export class TokenService {

  // Keys used in localStorage (like variable names)
  private readonly ACCESS_TOKEN  = 'shopapi_access_token';
  private readonly REFRESH_TOKEN = 'shopapi_refresh_token';
  private readonly USER_DATA     = 'shopapi_user';

  // ── Save both tokens after successful login ──
  saveTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem(this.ACCESS_TOKEN,  accessToken);
    localStorage.setItem(this.REFRESH_TOKEN, refreshToken);
  }

  // ── Get access token (used in HTTP header) ──
  getAccessToken(): string | null {
    return localStorage.getItem(this.ACCESS_TOKEN);
  }

  // ── Get refresh token (used to renew access token) ──
  getRefreshToken(): string | null {
    return localStorage.getItem(this.REFRESH_TOKEN);
  }

  // ── Save user info (name, email, roles) ──
  saveUser(user: any): void {
    localStorage.setItem(this.USER_DATA, JSON.stringify(user));
    // JSON.stringify converts object → string for storage
  }

  // ── Get saved user info ──
  getUser(): any {
    const data = localStorage.getItem(this.USER_DATA);
    return data ? JSON.parse(data) : null;
    // JSON.parse converts string back → object
  }

  // ── Remove everything (on logout) ──
  clearAll(): void {
    localStorage.removeItem(this.ACCESS_TOKEN);
    localStorage.removeItem(this.REFRESH_TOKEN);
    localStorage.removeItem(this.USER_DATA);
  }

  // ── Check if user is logged in ──
  isLoggedIn(): boolean {
    const token = this.getAccessToken();
    if (!token) return false;            // no token = not logged in
    return !this.isTokenExpired(token);  // expired token = not logged in
  }

  // ── Decode JWT to read claims inside it ──
  decodeToken(token: string): any {
    try {
      // JWT structure: header.payload.signature
      // We only need the payload (index 1)
      const payload = token.split('.')[1];

      // payload is Base64 encoded → decode it
      // atob() = browser function to decode Base64
      const decoded = atob(payload);

      // Convert JSON string → JavaScript object
      return JSON.parse(decoded);
    } catch {
      return null; // invalid token
    }
  }

  // ── Check if token has expired ──
  isTokenExpired(token: string): boolean {
    const decoded = this.decodeToken(token);
    if (!decoded?.exp) return true;

    // exp = expiry timestamp in SECONDS
    // Date.now() = current time in MILLISECONDS
    // Multiply exp by 1000 to convert to milliseconds
    return decoded.exp * 1000 < Date.now();
  }

  // ── Get user roles from JWT token ──
  getRoles(): string[] {
    const token = this.getAccessToken();
    if (!token) return [];

    const decoded = this.decodeToken(token);
    if (!decoded) return [];

    // ASP.NET Core uses this long claim name for roles
    const roleKey = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
    const role = decoded[roleKey];

    if (!role) return [];

    // role can be string (one role) or string[] (multiple roles)
    return Array.isArray(role) ? role : [role];
  }

  // ── Check if user has a specific role ──
  hasRole(role: string): boolean {
    return this.getRoles().includes(role);
  }

  // ── Get user ID from token ──
  getUserId(): string | null {
    const token = this.getAccessToken();
    if (!token) return null;
    const decoded = this.decodeToken(token);
    // ASP.NET Core uses this claim name for user ID
    const idKey = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
    return decoded?.[idKey] || null;
  }

  // ── Get user email from token ──
  getEmail(): string | null {
    const token = this.getAccessToken();
    if (!token) return null;
    const decoded = this.decodeToken(token);
    const emailKey = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';
    return decoded?.[emailKey] || null;
  }
}